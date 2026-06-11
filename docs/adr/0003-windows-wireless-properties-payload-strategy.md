# ADR-0003: Windows Wireless Properties Agent Payload Strategy

## Status

Accepted

## Context

Intellinode is adding REST APIs for **Windows Wireless Properties** (Network Settings → WiFi security profiles; Windows `:XP` only in v1), mirroring the **Keyboard Settings** admin API and task pipeline. A spike was required **before PR1** to measure realistic agent JSON payload sizes and choose between inline JSON (Keyboard / Computer Name parity) and server-side hydration (802.1X parity).

**Wireless Properties** manages WiFi security profiles (SSID, authentication, encryption, keys) — one profile per agent task (add/update/delete). It is **not** Wireless Setup (IP/DHCP), which uses `XPNetwork_Settings` in FusionX.

Unlike Computer Name (300–532 chars — see [ADR-0002](./0002-windows-computer-name-payload-strategy.md)), Wireless Properties payloads include up to four long credential/identity strings (SSID 128, network name 50, key 100, PPK 100) plus auth labels. Unlike 802.1X (3,494–11,809 chars — see [ADR-0001](./0001-windows-802-1x-payload-strategy.md)), payloads are scalar-only with no certificate arrays, but **max FusionX UI field lengths exceed the 512-char `device_tasks.function_parameter` limit**.

### Current Keyboard task flow (Intellinode)

End-to-end trace:

1. **`KeyboardSettingsService.QueueKeyboardWorkAsync`**
   - Upserts `device_keyboard_settings`.
   - Builds full FusionX-shaped JSON → `{"WinCELinux":{"XPKeyboard":{...}}}`.
   - Validates `functionPayload.Length <= 512`.
   - Creates `device_tasks` with full JSON inline in `FunctionParameter`.

2. **`AgentTaskService.GetPendingTasksAsync`**
   - Returns stored `FunctionParameter` verbatim for Keyboard — **no hydration**.
   - 802.1X tasks are hydrated via `Windows8021xTaskPayloadHydrator` (ADR-0001).

Typical Keyboard `functionParameter` size: **~129 chars**.

### Database constraints

| Column | Max length | Source |
|--------|------------|--------|
| `device_tasks.function_parameter` | **512** | `IntellinodeDbContext` → `HasMaxLength(512)` |
| `device_tasks.extra_data` | **512** | Same |
| `AgentValidators` (admin queue) | **512** | `FunctionParameter` rule |

### FusionX Wireless Properties delivery (parity target)

FusionX **Network Settings → Wireless Properties** (`UCWindowsWirelessProperties.ascx`, `WirelessProperties_Handler.ashx.cs`):

1. **`WindowsWirelessDAC`** (`InsertToDatabase` / `UpdateToDatabase` / `DeleteFromDatabase`)
   - Maps UI model → `WinCELinux.XPWirelessNetworkSecuritySettings` (`structXP_Data.cs` ~line 3671).
   - Serializes with `clsCommon.SerializeObject` → **binary `byte[]`** (`FunctionObject` blob).
   - Add/update: all struct fields from UI; `strStatus` = `objWifi.Status` (UI sets `string.Empty`).
   - Delete: blob contains **SSID only** (`strNetworkSSDIName`); other fields default empty.
   - Queues task via `prc_TaskManager_ExecuteNow_NEW` / `prc_TaskManager_ExecuteLater` with `@FunctionObject` = byte[].

2. **Agent poll**
   - Fetches blob by `FunctionObjectID`; deserializes `XPWirelessNetworkSecuritySettings`.
   - Module type string: `"Wireless Network Security"` (`Wirelss_Security_ModuleTypeMUI`).
   - Schedule signal (FusionX): `{macAddress}&WNS` (handler / ascx code-behind). UI delete confirmation popup references `XPWIFI` — see open questions.

3. **FusionX UI field limits** (`UCWindowsWirelessProperties.ascx`)
   - SSID (`txtW_networkname`): MaxLength **128**
   - Network name (`txtNetworkName`): MaxLength **50**
   - Network key (`txtW_networkkey`): MaxLength **100**
   - Pre-shared key (`TxtPreSharedKey`): MaxLength **100**
   - Longest auth label: **WPA2-Enterprise**

Intellinode will use **JSON** instead of binary blobs, wrapped as:

```json
{
  "WinCELinux": {
    "XPWirelessNetworkSecuritySettings": {
      "strNetworkSSDIName": "...",
      "strNetworkAuthentication": "WPA2-Personal",
      "strNetworkDataEncr": "AES",
      "strNetworkKey": "...",
      "strNetworkPPK": "...",
      "iNetworkKeyIndex": 1,
      "strNetworkName": "...",
      "strStatus": "",
      "Conn_Auto_WhenIn_Range": true,
      "Text1": "true",
      "Text2": "",
      "Text3": "",
      "TaskID": 0,
      "AgentAction": 0
    }
  }
}
```

**Contrast with Computer Name:** typical/min scenarios fit 512, but **max FusionX UI limits do not** (780 chars). **Contrast with 802.1X:** payloads are smaller but share the same “settings in DB, compact task reference, hydrate at poll” pattern when inline JSON exceeds 512.

### Password / key handling (PR2+)

- **Agent payload**: `strNetworkKey` and `strNetworkPPK` included (FusionX parity).
- **API GET**: write-only redaction (PR2) — keys not returned in current/history responses.

## Decision

**Adopt Option B: 802.1X parity — settings table (JSONB per profile) + compact task reference + server-side hydration at poll time.**

Fallback documented: **Option A** — inline full JSON in `device_tasks.function_parameter` (Keyboard / Computer Name parity). **Rejected** because max realistic single-profile payload at FusionX UI field limits is **780 chars** (+268 over 512); PR2 validators cannot honor FusionX max lengths and stay ≤512 without breaking parity.

### Why Option B (not Option A)

| Criterion | Option A (inline JSON) | Option B (JSONB + compact ref + hydration) |
|-----------|------------------------|--------------------------------------------|
| Payload size | Scenarios 2–3, 5, 6 fit 512; **scenario 4 (max UI limits) = 780** | Compact reference **22–73 chars** |
| FusionX field parity | Requires caps below UI MaxLength (128/100 SSID/key) | **Full UI limits in `settings_json`** |
| Agent compatibility | High — inline JSON on poll | **High** — hydration restores full JSON in API response |
| `GetPendingTasksAsync` | No changes | Module-specific `IWindowsWirelessPropertiesTaskPayloadHydrator` |
| Multi-profile per device | Each task self-contained | Version + profile key in reference; JSONB row per SSID/profile |
| Consistency | Keyboard / Computer Name | **802.1X pattern (ADR-0001)** |

**Hydration required** — `AgentTaskService.GetPendingTasksAsync` must expand compact `FunctionParameter` into full `{"WinCELinux":{"XPWirelessNetworkSecuritySettings":{...}}}` before returning tasks to the agent (same outward contract as inline modules).

### PR1+ task contract

| Field | Value |
|-------|-------|
| `ModuleName` | `Wireless Network Security` |
| `FunctionName` | `Now` / `Update` |
| `ExtraData` | `{macAddress}&WNS` (FusionX schedule signal; confirm with agent team — UI popup uses `XPWIFI`) |
| `FunctionParameter` (stored in DB) | `{"settingsVersion":<N>,"profileId":"<id>"}` — target **≤ 64 chars**; spike shows UUID `profileId` = **73 chars** (PR1 may use short numeric profile key or SSID hash) |
| Full agent JSON (API response) | Hydrated at `GetPendingTasksAsync` from per-profile `settings_json`, wrapped as `{"WinCELinux":{"XPWirelessNetworkSecuritySettings":{...}}}` |
| `SettingsKind` | `WindowsWirelessProperties` (PR1 enum) |
| Settings storage | `device_windows_wireless_properties_settings` (or equivalent) — **one row per device + profile (SSID)** with `settings_json` JSONB + version/apply columns |
| Hydration | `WindowsWirelessPropertiesTaskPayloadHydrator` (reuse `Windows8021xTaskPayloadHydrator` pattern) |
| OS v1 | `:XP` only |

## Payload size measurements

Measured with `System.Text.Json` (default naming), spike test `WindowsWirelessPropertiesPayloadSizeSpikeTests` in `tests/Intellinode.Infrastructure.Tests/WindowsWirelessProperties/`.

| Scenario | Serialized size (chars) | Fits 512? |
|----------|-------------------------|-----------|
| Keyboard comparable (`XPKeyboard`) | 129 | Yes |
| Min realistic — Open network (SSID Guest, no auth, no keys) | 350 | Yes |
| Typical — WPA2-Personal (16-char key, auto-connect) | 361 | Yes |
| Max realistic — FusionX UI limits (SSID 128, name 50, key 100, PPK 100, WPA2-Enterprise) | **780** | **No (+268 over)** |
| Delete operation — SSID only (FusionX `DeleteFromDatabase` DAC parity) | 336 | Yes |
| Option B compact reference `{"settingsVersion":42}` | 22 | Yes |
| Option B compact reference `{"settingsVersion":42,"profileId":"<uuid>"}` | 73 | Yes |

Typical WPA2-Personal sample (abbreviated):

```json
{
  "WinCELinux": {
    "XPWirelessNetworkSecuritySettings": {
      "strNetworkSSDIName": "Corp-WiFi",
      "strNetworkAuthentication": "WPA2-Personal",
      "strNetworkDataEncr": "AES",
      "strNetworkKey": "CorpWiFiKey!2024",
      "strNetworkPPK": "",
      "iNetworkKeyIndex": 1,
      "strNetworkName": "",
      "strStatus": "",
      "Conn_Auto_WhenIn_Range": true,
      "Text1": "true",
      "Text2": "",
      "Text3": "",
      "TaskID": 0,
      "AgentAction": 0
    }
  }
}
```

Delete sample (FusionX DAC sends SSID only; remaining fields serialize as empty/default):

```json
{
  "WinCELinux": {
    "XPWirelessNetworkSecuritySettings": {
      "strNetworkSSDIName": "Corp-WiFi-To-Remove",
      "strNetworkAuthentication": "",
      "strNetworkDataEncr": "",
      "strNetworkKey": "",
      "strNetworkPPK": "",
      "iNetworkKeyIndex": 0,
      "strNetworkName": "",
      "strStatus": "",
      "Conn_Auto_WhenIn_Range": false,
      "Text1": "",
      "Text2": "",
      "Text3": "",
      "TaskID": 0,
      "AgentAction": 0
    }
  }
}
```

## FusionX parity

| Field | FusionX | Intellinode (proposed) |
|-------|---------|------------------------|
| UI module | Network Settings → Wireless Properties | Same scope (not Wireless Setup / IP) |
| Module name (`ModuleType`) | `Wireless Network Security` | `Wireless Network Security` |
| Function names | Execute-now / queue via schedule | `Now` / `Update` |
| Signal (schedule `ExtraData`) | `{mac}&WNS` | `{mac}&WNS` (pending agent confirmation vs UI `XPWIFI`) |
| Wrapper key | `XPWirelessNetworkSecuritySettings` | `XPWirelessNetworkSecuritySettings` |
| Payload wire format | Binary `FunctionObject` blob | JSON `{"WinCELinux":{"XPWirelessNetworkSecuritySettings":{…}}}` |
| Payload storage | SQL scalar + blob per operation | JSONB `settings_json` per device/profile + compact version reference on task |
| Agent delivery | Fetch blob at poll by `FunctionObjectID` | Hydrate JSON at poll from settings row by `settingsVersion` + `profileId` |
| Delete agent payload | SSID-only struct | Same shape (hydrated from stored delete snapshot or minimal JSONB) |
| Keys in agent payload | Yes (`strNetworkKey`, `strNetworkPPK`) | Yes (in `settings_json`, hydrated into response) |
| Keys in API GET | N/A (legacy UI) | Write-only redaction (PR2) |
| Multi-profile | One SSID per task | One SSID per task (PR1 schema per profile) |

## Consequences

### Positive

- Honors FusionX UI max field lengths without widening `device_tasks` for all modules.
- Preserves agent polling contract (`functionParameter` still contains apply-ready JSON after hydration).
- Reuses proven 802.1X hydration pattern (`Windows8021xTaskPayloadHydrator` as template).
- Typical and delete payloads are small, but max-length WPA2-Enterprise profiles remain supported.

### Negative

- Diverges from Keyboard / Computer Name inline simplicity.
- `GetPendingTasksAsync` gains another module-specific hydrator.
- Multi-profile storage (one row per SSID) is more complex than single-row modules (Keyboard, 802.1X per device).
- Compact `profileId` encoding needs PR1 design (UUID = 73 chars vs 64-char 802.1X cap).

### Risks

- **Signal mismatch** — FusionX schedule uses `WNS`; UI delete popup uses `XPWIFI`. Wrong suffix breaks agent routing.
- **Race** — settings updated after task queued but before agent polls — bind task to `settingsVersion` at queue time (same mitigation as ADR-0001).
- **Delete semantics** — FusionX delete blob is SSID-only; confirm agent does not require `strStatus` discriminator.
- **Option A false hope** — capping fields to fit 512 would violate FusionX UI MaxLength parity.

## Follow-up PRs

- **PR1**: Domain entity `DeviceWindowsWirelessPropertiesSettings` (per device + profile/SSID), migration (`settings_json` JSONB), `SettingsKind.WindowsWirelessProperties`, contracts/options stubs, payload builder + `IWindowsWirelessPropertiesTaskPayloadHydrator` interface (no controller).
- **PR2**: `WindowsWirelessPropertiesSettingsService` (queue/execute-now/current/history per profile), compact `FunctionParameter`, key write-only masking on GET, pending-task blocking per module.
- **PR3**: `AdminWindowsWirelessPropertiesController`, `AgentTaskService` hydration for `Wireless Network Security` tasks, `WindowsWirelessPropertiesTaskAckHandler`, HTTP samples.
- **PR4**: Apply history, integration tests, multi-profile list/delete flows.
- **PR5**: Bulk/group apply, SysView/template library — out of v1 scope.

## Appendix: Open questions for agent team

Do not block PR1 design; confirm before PR3 agent integration:

1. **Signal suffix** — FusionX schedule code uses `{mac}&WNS`; delete confirmation UI passes `XPWIFI`. Which suffix must Intellinode `ExtraData` use?
2. **Delete payload** — Is SSID-only struct sufficient, or does the agent expect `strStatus` / `AgentAction` values for delete?
3. **Delete batching** — FusionX queues one SSID per delete task; confirm no batch-delete struct expected.
4. **Module name string** — Exact match required for `"Wireless Network Security"` handler registration?
5. **`Text1` semantics** — FusionX maps `chkConnectnetworkboradcasting` to `"true"` / `"false"` string; confirm agent parsing.
6. **`strStatus` on add/update** — FusionX sets empty string; any non-empty values used in the field today?

---

**Ready for PR1: Yes**

Blockers: none for PR1 design. PR1 must resolve compact `profileId` encoding (≤64 chars preferred, or document 73-char UUID reference). Confirm signal suffix (`WNS` vs `XPWIFI`) with agent team before PR3.
