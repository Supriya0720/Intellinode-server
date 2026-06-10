# ADR-0001: Windows 802.1X Agent Payload Strategy

## Status

Accepted

## Context

Intellinode is adding REST APIs for **Windows 802.1X Security** (Windows `:XP` only in v1), mirroring the **Keyboard Settings** admin API and task pipeline. A spike was required because 802.1X agent payloads are far larger than the current `device_tasks.function_parameter` column allows.

### Current Keyboard task flow (Intellinode)

End-to-end trace:

1. **`KeyboardSettingsService.QueueKeyboardWorkAsync`**
   - Upserts `device_keyboard_settings` (scalar columns + `settings_version`, `pending_apply`).
   - Builds the full FusionX-shaped JSON via `BuildLegacyKeyboardPayload` → `{"WinCELinux":{"XPKeyboard":{...}}}`.
   - Validates `functionPayload.Length <= 512` (`MaxFunctionParameterLength`).
   - Creates `device_tasks` row:
     - `ModuleName` = `"Keyboard"`
     - `FunctionName` = `"Now"` (instant) or `"Update"` (queued)
     - `FunctionParameter` = full JSON payload (inline)
     - `ExtraData` = `"{macAddress}&{signalSuffix}"` (default suffix `SCR` from `KeyboardOptions`)
     - `Status` = `Pending`
   - Writes `device_settings_apply_logs` with `SettingsKind.Keyboard`.

2. **`AgentTaskService.GetPendingTasksAsync`**
   - Loads `Pending` + `InProcess` tasks ordered by `CreatedUtc`.
   - Marks the first `Pending` task as `InProcess` (FusionX-like).
   - Maps each task to `AgentPendingTaskDto`:
     - `FunctionParameter` = stored value verbatim
     - `Signal` = `DeviceTaskOperations.ExtractSignal(ExtraData)` (e.g. `AA:BB:...:XP&SCR`)

3. **`AgentsController`**
   - `GET /api/v1/agents/tasks/pending` → `GetPendingTasksAsync`
   - `POST /api/v1/agents/tasks/ack` → `AcknowledgeTasksAsync`

4. **`KeyboardTaskAckHandler.ApplyAckAsync`**
   - On `Completed`: sets `last_applied_version`, clears `pending_apply`, writes `Applied` apply log.
   - On `Failed`: clears `pending_apply`, stores truncated reason, writes `Failed` apply log.
   - Does **not** re-read `FunctionParameter`; state comes from `device_keyboard_settings`.

**What the agent receives today for Keyboard tasks:**

```json
{
  "tasks": [
    {
      "id": "…",
      "legacyTaskId": 1,
      "moduleName": "Keyboard",
      "functionName": "Now",
      "functionParameter": "{\"WinCELinux\":{\"XPKeyboard\":{\"iDelay\":2,\"iRepeat_Rate\":31,\"Locale\":\"English (United States)\",\"IsReplaceExistingKeyboard\":false}}}",
      "signal": "AA:BB:CC:DD:EE:10:XP&SCR",
      "status": "InProcess"
    }
  ]
}
```

Typical Keyboard `functionParameter` size: **~129 chars** (well under the 512 limit).

### Database constraints

| Column | Max length | Source |
|--------|------------|--------|
| `device_tasks.function_parameter` | **512** | `IntellinodeDbContext` → `HasMaxLength(512)` |
| `device_tasks.extra_data` | **512** | Same |
| `AgentValidators` (admin queue) | **512** | `FunctionParameter` rule |

`DisplaySettingsService` defines `MaxFunctionParameterLength = 2048` in code, but the **database column remains 512** — an existing inconsistency that must be resolved before any “just increase the limit” approach.

### FusionX 802.1X delivery (parity target)

FusionX does **not** inline 802.1X JSON in task function parameters.

1. **`Windows802_1X_SecurityDAC.UpdateToDatabase`**
   - Maps UI model → `WinCELinux.Windows_802_1x` struct (50+ fields, cert arrays, EKU lists).
   - Serializes with `clsCommon.SerializeObject(objWin8021x)` → **binary `byte[]`** (`BinaryFormatter`).
   - Persists settings to `Win802_1x_InsertUpdate` stored proc (scalar columns per device).
   - Queues task via `prc_TaskManager_ExecuteNow_NEW` / `prc_TaskManager_ExecuteLater` with **`@FunctionObject` = byte[]** (not a string parameter).

2. **`Function.FetchObjectFromDatabase(FunctionObjectID, "Windows_802_1x", strFQDN)`**
   - Reads `FunctionObject` blob from DB by `FunctionObjectID` + module name.
   - Deserializes to `Windows_802_1x` struct.

3. **`WinService_AuthToken.asmx.cs` — `case "Windows_802_1x"`**
   - At agent poll time, fetches blob by `FunctionObjectID`.
   - Sets `TaskID`, `AgentAction` on struct.
   - Appends signal module type `"Win802_1x"`.

4. **Signal / module naming (FusionX)**
   - `ModuleName` / schedule module: `"Windows_802_1x"`
   - `ExtraData` / signal: `"{macAddress}&Win802_1x"`

### FusionX Keyboard delivery (comparison)

FusionX Keyboard uses the **same blob pattern** as 802.1X:

- `WindowsKeyboardDAC` → `SerializeObject(XPKeyboard)` → `@FunctionObject` byte[]
- Agent poll → `FetchObjectFromDatabase(FunctionObjectID, "Keyboard")`

Intellinode **simplified** Keyboard for the REST era by inlining JSON in `function_parameter` (small enough to fit 512 chars). That shortcut does not transfer to 802.1X.

### Password handling

802.1X settings include `cPassword` (and domain credentials). Requirements:

- **API GET responses**: password fields are **write-only** — never returned in `GET current` or history; use a sentinel such as `"********"` or omit the field.
- **Stored JSON** (`settings_json`): password **is stored** (encrypted at rest is a follow-up hardening item; out of PR0 scope) because the agent must receive credentials to apply PEAP/MSCHAP profiles.
- **Task `function_parameter` (Option A)**: compact reference contains **no password**; hydration reads from `settings_json` at poll time.

## Decision

**Adopt Option A (settings table + compact task reference + server-side hydration at poll time).**

Fallback: **Option B** — increase `function_parameter` to 16 KB and inline full JSON (Keyboard parity, simpler but weaker for large cert lists and future network modules).

### PR1+ task contract

| Field | Value |
|-------|-------|
| `ModuleName` | `Windows_802_1x` |
| `FunctionName` | `Now` / `Update` |
| `ExtraData` | `{macAddress}&Win802_1x` |
| `FunctionParameter` (stored in DB) | `{"settingsVersion":<N>}` — **≤ 64 chars** |
| Full agent JSON (API response) | Hydrated at `GetPendingTasksAsync` from `device_windows_802_1x_settings.settings_json`, wrapped as `{"WinCELinux":{"Windows_802_1x":{...}}}` |
| `SettingsKind` | Add `Windows8021x` enum value |
| Settings storage | `device_windows_802_1x_settings.settings_json` (JSONB) + scalar version/apply columns mirroring Keyboard |

### Why Option A

| Criterion | Option A | Option B (16 KB) | Option C (payload table) | Option D (ExtraData) |
|-----------|----------|------------------|--------------------------|----------------------|
| Agent compatibility | **High** — agent still receives full JSON in `functionParameter` on poll; no new agent fetch endpoint | High — inline JSON | Medium — needs hydration or second fetch | **No** — ExtraData capped at 512 |
| DB migration | New settings table + enum; task column unchanged | Alter `function_parameter` on all tasks | New table + join logic | None useful |
| Keyboard pattern consistency | API/service flow matches; storage diverges (justified by size) | Closest to Keyboard inline pattern | New pattern | N/A |
| Security | Password only in settings JSON, not in task row | Password duplicated in every task row | Same as A or B | N/A |
| Future reuse (Ethernet, Wireless) | **Strong** — same JSONB + hydration pattern | Moderate — may hit 16 KB again | Strong | No |

### Agent changes

- **No change to the task polling contract** (Option A hydration preserves `functionParameter` as full JSON in the API response).
- **Yes — agent feature work is required before PR3**: the agent must implement a `Windows_802_1x` module handler (parse JSON, apply settings, ack). This is independent of payload storage; it is required for any option.
- Intellinode server changes in PR3: `AgentTaskService.GetPendingTasksAsync` hydration + `Windows8021xTaskAckHandler`.

## Payload size measurements

Measured with `System.Text.Json` (default naming), spike test `Windows8021xPayloadSizeSpikeTests` in `tests/Intellinode.Infrastructure.Tests/Windows8021x/`.

| Scenario | Serialized size (chars) | Fits 512? |
|----------|-------------------------|-----------|
| Keyboard comparable (`XPKeyboard`) | 129 | Yes |
| 802.1X min realistic (PEAP wireless, 3 trusted roots, 2 EKUs) | **3,494** | **No** (~6.8× over) |
| 802.1X max realistic (wired + smart card, 15+10+8 certs, 12+8 EKUs) | **11,809** | **No** (~23× over) |
| Option A compact reference `{"settingsVersion":42}` | ~28 | Yes |

Min realistic sample (abbreviated):

```json
{
  "WinCELinux": {
    "Windows_802_1x": {
      "blEnable802_Authentication": true,
      "str_Authentication": "Microsoft: Protected EAP (PEAP)",
      "isWired": false,
      "cSSID": "Corp-WiFi-Enterprise",
      "cPassword": "…",
      "objTrusted_Root_Certificate_Authorities_PEAP_TLS": [ { "thumprint": "…" }, … ],
      "ObjList_Of_EKUs_Client_Authentication": [ … ]
    }
  }
}
```

## FusionX parity

| Field | FusionX | Intellinode (proposed) |
|-------|---------|------------------------|
| Module name | `Windows_802_1x` | `Windows_802_1x` |
| Function names | Execute-now / queue via schedule | `Now` / `Update` |
| Signal | `{mac}&Win802_1x` | `{mac}&Win802_1x` |
| Payload wire format | Binary `FunctionObject` blob (deserialized to struct) | JSON `{"WinCELinux":{"Windows_802_1x":{…}}}` (FusionX struct field names) |
| Payload storage | SQL blob + `FunctionObjectID` on task | JSONB `settings_json` + `settingsVersion` reference on task |
| Agent delivery | Fetch blob at poll by `FunctionObjectID` | Hydrate JSON at poll from settings table by `settingsVersion` |
| Settings persistence | `Win802_1x_InsertUpdate` scalar + blob | `device_windows_802_1x_settings` row per device |
| Password in agent payload | Yes (`cPassword` in struct) | Yes (in `settings_json`, hydrated into response) |
| Password in API GET | N/A (legacy UI) | Write-only (not returned) |

## Consequences

### Positive

- Unblocks 802.1X without widening `device_tasks` for all modules.
- Preserves agent polling contract (`functionParameter` still contains apply-ready JSON).
- Aligns with FusionX’s “settings in DB, task carries reference” model while using JSON instead of binary serialization.
- Reusable pattern for Ethernet Setup, Wireless Properties, and other large network modules.

### Negative

- Diverges from Keyboard’s “store exact payload in task row” simplicity.
- `GetPendingTasksAsync` gains module-specific hydration logic (or a shared `ITaskPayloadHydrator`).
- Historical task rows only store a version reference; exact applied payload requires joining settings snapshot or apply log (acceptable; same as FusionX blob indirection).

### Risks

- **Race**: settings updated after task queued but before agent polls — mitigate by binding task to `settingsVersion` at queue time and hydrating that version (store version in reference; reject/hydrate stale version explicitly).
- **Display 2048 vs DB 512 mismatch** — fix in a dedicated migration before any Option B fallback.
- **Password at rest** — `settings_json` contains credentials; document encryption/TDE expectations for production.

## Follow-up PRs

- **PR1**: Domain entity `DeviceWindows8021xSettings`, migration (`settings_json` JSONB), `SettingsKind.Windows8021x`, contracts/options stubs, payload builder + hydration interface (no controller).
- **PR2**: `Windows8021xSettingsService` (queue/execute-now/current/history), compact `FunctionParameter`, password write-only masking on GET.
- **PR3**: `AdminWindows8021xController`, `AgentTaskService` hydration for `Windows_802_1x` tasks, `Windows8021xTaskAckHandler`, HTTP samples.
- **PR4**: Apply history, integration tests, pending-task blocking.
- **PR5**: Bulk/group apply, edge cases, ops docs — see [docs/windows-802-1x-operations.md](../windows-802-1x-operations.md).

---

**Ready for PR1: Yes**

Blockers: none for PR1 design. Confirm with agent team that `Windows_802_1x` JSON field names match existing FusionX struct expectations (spike sample uses struct names from `structXP_Data.cs`). Resolve Display `function_parameter` 512/2048 inconsistency if Option B is ever needed as fallback.
