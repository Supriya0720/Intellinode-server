# ADR-0002: Windows Computer Name Agent Payload Strategy

## Status

Accepted

## Context

Intellinode is adding REST APIs for **Windows Computer Name / Domain Join** (Windows `:XP` only in v1), mirroring the **Keyboard Settings** admin API and task pipeline. A spike was required **before PR1** (domain/persistence) to measure realistic agent JSON payload sizes and choose between inline JSON (Keyboard parity) and server-side hydration (802.1X parity).

Unlike 802.1X (3,494–11,809 chars — see [ADR-0001](./0001-windows-802-1x-payload-strategy.md)), Computer Name payloads are structurally small: two FusionX structs with scalar string fields and no certificate arrays. The spike determines whether they fit the existing **`device_tasks.function_parameter` 512-char limit** without hydration.

### Current Keyboard task flow (Intellinode)

End-to-end trace:

1. **`KeyboardSettingsService.QueueKeyboardWorkAsync`**
   - Upserts `device_keyboard_settings` (scalar columns + `settings_version`, `pending_apply`).
   - Builds full FusionX-shaped JSON via `BuildLegacyKeyboardPayload` → `{"WinCELinux":{"XPKeyboard":{...}}}`.
   - Validates `functionPayload.Length <= 512` (`MaxFunctionParameterLength`).
   - Creates `device_tasks` row:
     - `ModuleName` = `"Keyboard"`
     - `FunctionName` = `"Now"` (instant) or `"Update"` (queued)
     - `FunctionParameter` = full JSON payload (inline)
     - `ExtraData` = `"{macAddress}&{signalSuffix}"` (default suffix `SCR`)
     - `Status` = `Pending`

2. **`AgentTaskService.GetPendingTasksAsync`**
   - Returns stored `FunctionParameter` verbatim — **no hydration**.

3. **`KeyboardTaskAckHandler.ApplyAckAsync`**
   - Updates `device_keyboard_settings` apply state from ack; does not re-read `FunctionParameter`.

Typical Keyboard `functionParameter` size: **~129 chars** (well under the 512 limit).

### Database constraints

| Column | Max length | Source |
|--------|------------|--------|
| `device_tasks.function_parameter` | **512** | `IntellinodeDbContext` → `HasMaxLength(512)` |
| `device_tasks.extra_data` | **512** | Same |
| `AgentValidators` (admin queue) | **512** | `FunctionParameter` rule |

### FusionX Computer Name delivery (parity target)

FusionX **Network Settings → Computer Name** has two apply modes:

| UI mode | FusionX `Type` | Serialized struct | `ModuleName` |
|---------|----------------|-------------------|--------------|
| Host rename only | `UPDATE HOST` | `WindowsComputerNameSettings` | `Host Name` |
| Domain / workgroup join | `UPDATE ALL` | `WindowsDomainSettings` | `DomainSettings` |

Flow:

1. **`WindowsNetworkSetup_Handler.ashx.cs`** — `LoadComputerName`, `applyComputerNameSetting`
   - Host-only → `WindowsComputerNameSettings`; domain join → `WindowsDomainSettings`.
   - Sets `Type` to `UPDATE HOST` or `UPDATE ALL`.

2. **`WindowsComputerName_DAC.cs`**
   - Maps UI model → struct.
   - Serializes with `clsCommon.SerializeObject` → **binary `byte[]`** (`FunctionObject` blob).
   - Queues task via Task Manager with **`@FunctionObject` = byte[]** (not inline string).

3. **Agent poll**
   - Fetches blob by `FunctionObjectID`; deserializes struct.
   - Signal: `{macAddress}&CN` (from `setComputerNameObject` / `ModulesWindows.js` → `loadXPCompName`, `hdnMenu=CN`).

4. **Module names (FusionX MUI)**
   - `SetComputerNameModuleTypeMUI` = `"Host Name"`
   - `SetDomainSettingsModuleTypeMUI` = `"DomainSettings"`

Intellinode will use **JSON** instead of binary blobs, wrapped as:

```json
{ "WinCELinux": { "WindowsComputerNameSettings": { ... } } }
{ "WinCELinux": { "WindowsDomainSettings": { ... } } }
```

**Contrast with 802.1X:** payloads are orders of magnitude smaller; hydration (ADR-0001 Option A) is unnecessary for typical and realistic admin scenarios. **Contrast with Keyboard:** same inline JSON pattern applies when payloads fit 512.

### Password handling (PR2+)

- **Agent payload**: `Password` field included (FusionX parity).
- **API GET**: write-only redaction (PR2) — not returned in current/history responses.

## Decision

**Adopt Option A: Keyboard parity — inline full JSON in `device_tasks.function_parameter` (no hydration).**

Fallback documented: **Option B** — settings table + compact task reference + server-side hydration at poll time (802.1X / ADR-0001 pattern). Reserved if PR2 field validators cannot keep payloads ≤512 in practice.

### Why Option A (not Option B)

| Criterion | Option A (inline JSON) | Option B (JSONB + compact ref + hydration, 802.1X style) |
|-----------|------------------------|------------------------------------------------------------|
| Payload size | **4/5 spike scenarios fit 512**; worst-case unbounded spike is 532 (+20 over) — mitigated by PR2 max-length validators | Overkill for scalar-only structs |
| Agent compatibility | **High** — same poll contract as Keyboard | High but unnecessary complexity |
| Consistency with Keyboard | **Yes** | No |
| Future Ethernet/Wireless | N/A for Computer Name | Reuse 802.1X pattern for other large modules |
| `GetPendingTasksAsync` changes | **None** | Module-specific hydrator required |

**No hydration** — unlike ADR-0001, `AgentTaskService.GetPendingTasksAsync` returns the stored `FunctionParameter` verbatim for Computer Name tasks.

### PR1+ task contract

| Field | Value |
|-------|-------|
| `ModuleName` (host rename) | `Host Name` |
| `ModuleName` (domain join) | `DomainSettings` |
| `FunctionName` | `Now` / `Update` |
| `ExtraData` | `{macAddress}&CN` |
| `FunctionParameter` | Full inline JSON (≤ 512 chars after PR2 validation) |
| `SettingsKind` | `WindowsComputerName` (PR1 enum) |
| OS v1 | `:XP` only |
| Hydration | **None** |

Constants stub: `WindowsComputerNameModuleConstants` in `WindowsComputerNameContracts.cs`.

## Payload size measurements

Measured with `System.Text.Json` (default naming), spike test `WindowsComputerNamePayloadSizeSpikeTests` in `tests/Intellinode.Infrastructure.Tests/WindowsComputerName/`.

| Scenario | Serialized size (chars) | Fits 512? |
|----------|-------------------------|-----------|
| Keyboard comparable (`XPKeyboard`) | 129 | Yes |
| Min host rename (`WindowsComputerNameSettings`, `CORP-PC-01`) | 300 | Yes |
| Host rename with auto-generate metadata (`prefix`/`postfix`/`noOfChar`) | 299 | Yes |
| Min domain join (`WindowsDomainSettings`, PEAP-style creds) | 349 | Yes |
| Max domain join (15-char hostname, long domain, ~120-char OU, 64-char password) | **532** | **No (+20 over)** |

Min host rename sample (abbreviated):

```json
{
  "WinCELinux": {
    "WindowsComputerNameSettings": {
      "MacAddr": "AA:BB:CC:DD:EE:10:XP",
      "HostName": "CORP-PC-01",
      "Domain": "",
      "WorkGroup": "",
      "UserName": "",
      "Password": "",
      "prefix": "",
      "postfix": "",
      "noOfChar": 0,
      "IsMacOrSrNo": false,
      "Text1": "",
      "Text2": "",
      "Text3": "",
      "Text4": "",
      "Text5": "",
      "TaskID": 0,
      "AgentAction": 0
    }
  }
}
```

Min domain join sample (abbreviated):

```json
{
  "WinCELinux": {
    "WindowsDomainSettings": {
      "MacAddr": "AA:BB:CC:DD:EE:10:XP",
      "IsDomainWorkgroup": "True",
      "HostName": "CORP-PC-01",
      "Domain": "corp.example.com",
      "UserName": "jsmith",
      "Password": "P@ssw0rd!2024",
      "OrganizationalUnit": "OU=Devices,DC=corp,DC=local"
    }
  }
}
```

**Spike blocker (worst-case only):** unbounded `OrganizationalUnit` + 64-char `Password` + long `Domain` produces 532 chars. **Mitigation (PR2):** enforce max lengths so serialized payload ≤512 (e.g. cap OU ~100 chars, password 64, domain/hostname per Windows limits). Option B hydration remains documented fallback if caps are rejected.

## FusionX parity

| Field | FusionX | Intellinode (proposed) |
|-------|---------|------------------------|
| Host module | `Host Name` | `Host Name` |
| Domain module | `DomainSettings` | `DomainSettings` |
| Function names | Execute-now / queue | `Now` / `Update` |
| Signal | `{mac}&CN` | `{mac}&CN` |
| Payload wire format | Binary `FunctionObject` blob | JSON `WinCELinux.WindowsComputerNameSettings` / `WindowsDomainSettings` |
| Storage | SQL scalar + blob | `device_windows_computer_name_settings` scalars (PR1) |
| Password in agent payload | Yes | Yes |
| Password in API GET | N/A | Write-only redaction (PR2) |
| Agent delivery | Fetch blob at poll | Inline JSON in `functionParameter` (no hydration) |

## Consequences

### Positive

- Reuses proven Keyboard task pipeline — no `AgentTaskService` hydration changes.
- Settings persisted in scalar columns (PR1); task row is self-contained apply payload.
- Realistic admin scenarios (min host, auto-generate, min domain join) fit 512 with headroom.
- Two module names map cleanly to FusionX host vs domain apply modes.

### Negative

- PR2 must enforce field max lengths to guarantee ≤512 under all valid API inputs.
- Two `ModuleName` values → pending-task blocking is **per module** (host rename vs domain join are independent queues).
- Auto-generate hostname at group apply deferred to PR5.

### Risks

- **Long `OrganizationalUnit`** may approach 512 — mitigate with validator max length in PR2 (spike worst-case +20 chars).
- **Two module names** — admin UI must queue the correct module; pending-task checks are not shared with 802.1X/Ethernet.
- **Auto-generate hostname** at bulk/group apply — deferred to PR5; spike covers metadata fields only.
- **Option B fallback** — if PR2 caps are insufficient, adopt ADR-0001 hydration pattern (unlikely given spike margins).

## Follow-up PRs

- **PR1**: Entity, migration, payload builder, options stubs, `SettingsKind.WindowsComputerName`, `WindowsComputerNameModuleConstants`.
- **PR2**: Service, validators (including payload ≤512 enforcement), password write-only redaction on GET.
- **PR3**: Controller, ack handler, HTTP samples.
- **PR4**: Integration tests, pending-task blocking per module.
- **PR5**: Bulk/group apply, auto-generate hostnames, ops docs.

---

**Ready for PR1: Yes**

Blockers: none for PR1 design. PR2 validators must cap field lengths so worst-case serialized payload ≤512 (spike documents +20 overflow when unbounded). Confirm with agent team that JSON wrapper keys `WindowsComputerNameSettings` / `WindowsDomainSettings` and module names `Host Name` / `DomainSettings` match existing FusionX agent handlers.
