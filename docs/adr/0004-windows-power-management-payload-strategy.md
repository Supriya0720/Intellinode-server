# ADR-0004: Windows Power Management Agent Payload Strategy

## Status

Accepted

## Context

Intellinode is adding REST APIs for **Windows Power Management** (FusionX **System Settings → Power Management**; Windows `:XP` only in v1), mirroring the **Keyboard Settings** admin API and task pipeline. A spike was required **before PR1** to measure realistic agent JSON payload sizes and choose between inline JSON (Keyboard / Computer Name parity) and server-side hydration (802.1X / Wireless Properties parity).

**Power plan configuration** (`ModuleName` = `"Power Management Settings"`, struct `WinCELinux.XPPowerManagement`) is separate from generic **Power Management** agent tasks (Shutdown/Reboot with `moduleName` = `"Power Management"`) — do not merge.

Unlike Computer Name (300–532 chars — see [ADR-0002](./0002-windows-computer-name-payload-strategy.md)), power plan payloads include nested `objPowerOptions[]` / `objPowerSettings[]` arrays. Unlike 802.1X (3,494–11,809 chars — see [ADR-0001](./0001-windows-802-1x-payload-strategy.md)), payloads have no certificate blobs, but **full FusionX basic UI (5 option groups) and advanced trees exceed the 512-char `device_tasks.function_parameter` limit**.

### Current Keyboard task flow (Intellinode)

End-to-end trace:

1. **`KeyboardSettingsService.QueueKeyboardWorkAsync`**
   - Upserts `device_keyboard_settings`.
   - Builds full FusionX-shaped JSON → `{"WinCELinux":{"XPKeyboard":{...}}}`.
   - Validates `functionPayload.Length <= 512`.
   - Creates `device_tasks` with full JSON inline in `FunctionParameter`.

2. **`AgentTaskService.GetPendingTasksAsync`**
   - Returns stored `FunctionParameter` verbatim for Keyboard — **no hydration**.
   - 802.1X and Wireless Properties tasks are hydrated via module-specific hydrators (ADR-0001 / ADR-0003).

Typical Keyboard `functionParameter` size: **~129 chars**.

### Database constraints

| Column | Max length | Source |
|--------|------------|--------|
| `device_tasks.function_parameter` | **512** | `IntellinodeDbContext` → `HasMaxLength(512)` |
| `device_tasks.extra_data` | **512** | Same |
| `AgentValidators` (admin queue) | **512** | `FunctionParameter` rule |

### FusionX Power Management delivery (parity target)

FusionX **System Settings → Power Management** (`UCWindowsPowerManagement.ascx`, `WindowsPowerMngmntHandler.ashx.cs`, `WindowsPowerManagementDAC.cs`):

1. **`WindowsPowerManagementDAC.UpdateToDatabase`**
   - Persists plan XML to `XP_PowerPlan` / `XP_PowerPlan_AdvanceDetails` and changed options to dummy/changed tables.
   - Maps DB state → `WinCELinux.XPPowerManagement` via `GetPowerManagement_Settings` (plan name, `blIsActive`, `objPowerOptions[]`, `Operation`, `Index`).
   - Serializes with `clsCommon.SerializeObject(objStructwinPowerMangement)` → **binary `byte[]`** (`FunctionObject` blob).
   - Queues task via `prc_TaskManager_ExecuteNow_NEW` / `prc_TaskManager_ExecuteLater` with `@FunctionObject` = byte[].

2. **Schedule / task metadata (`SetObjectValues` in handler)**
   - `objSchedule.ModuleType` = `"Power Management Settings"` (`PowerOptionModuleTypeMUI` in `clsPreDefinedConditions.cs`)
   - `objSchedule.ModuleName` = **power plan name** (e.g. `Balanced`) from `SplitPlan(selectedddlPowerplan)` — stored in `XP_PowerPlan.Power_Plan`; schedule lookups use plan name, not the literal string `XP_PowerPlan`
   - `objPower.Parameter` = `PowerManagementAgentAction` → passed as `@AgentAction` on ExecuteNow/Later (numeric agent action from UI)
   - `objSchedule.Signal` = `{macAddress}&PMO,{planName}` (handler L3095)

3. **Basic option groups (handler template replay ~L595–654)**
   - **Display** → `Turn off display after`
   - **Hard disk** → `Turn off hard disk after`
   - **Sleep** → `Sleep after`
   - **Power buttons and lid** → `Power button action`, `Sleep button action`
   - **System standby** → `System standby`

4. **Advanced options** — `AdvancePowerOption.aspx` tree (USB selective suspend, hybrid sleep, processor states, media sharing, etc.) persisted in `XP_PowerPlan_AdvanceDetails.CurrentPlan_Xml`.

5. **Agent struct** (`structXP_Data.cs` ~L3594):

```csharp
public struct XPPowerManagement {
    public string strPowerSchemaName;
    public bool blIsActive;
    public PowerOptions[] objPowerOptions;
    public string Operation;
    public string Index;
    public int TaskID;
    public int AgentAction;
}
```

6. **DAC value normalization** — `getPowerSettings` strips `" Minutes"` suffix to numeric string when value contains `"Minutes"` (agent may receive `"10"` not `"10 minutes"`). Spike uses human-readable strings; PR2 builder must confirm agent expectation.

7. **Agent poll (FusionX AppService)** — schedule poll loads `objXPPowerManagement` via `PollMethods.GetXPSchedulewisePowerManagement(macAddress, "Power Management Settings", ...)`. Separate read path serializes struct to JSON (unwrapped) for encrypted responses. Intellinode will use **`{"WinCELinux":{"XPPowerManagement":{…}}}`** wrapper like other REST-era modules.

Intellinode target JSON (confirmed field names from FusionX struct):

```json
{
  "WinCELinux": {
    "XPPowerManagement": {
      "strPowerSchemaName": "Balanced",
      "blIsActive": true,
      "objPowerOptions": [
        {
          "strPowerOptionName": "Display",
          "objPowerSettings": [
            { "strSettingName": "Turn off display after", "strSettingValue": "10 minutes" }
          ]
        }
      ],
      "Operation": "Update",
      "Index": "1",
      "TaskID": 42,
      "AgentAction": 0
    }
  }
}
```

**Contrast with Computer Name:** min/typical basic scenarios fit 512, but **full basic UI (5 groups) does not** (881 chars). **Contrast with 802.1X:** same “settings in DB, compact task reference, hydrate at poll” pattern when inline JSON exceeds 512.

### Separation: Power Management Settings vs Power Management actions

| | Plan settings (this ADR) | Agent power actions |
|--|--------------------------|---------------------|
| FusionX module | `Power Management Settings` | `Power Management` |
| Purpose | Apply power plan/options | Shutdown, reboot, etc. |
| Intellinode | PR1–PR4 | Existing generic task queue (`moduleName`: `"Power Management"`, `functionName`: `"Shutdown"`, …) |

## Decision

**Adopt Option B: 802.1X / Wireless Properties parity — settings table (JSONB) + compact task reference + server-side hydration at poll time.**

Fallback documented: **Option A** — inline full JSON in `device_tasks.function_parameter` (Keyboard / Computer Name parity). **Rejected for PR2+** because spike scenario 4 (full FusionX basic UI, 5 option groups) = **881 chars** (+369 over 512). Scenarios 2–3 fit 512 but do not represent full basic parity.

### Spike decision rules applied

| Rule | Result |
|------|--------|
| Scenarios 2–4 all ≤512 → Option A for PR2 basic | **Not met** — scenario 4 = 881 |
| Scenario 4 or 5 >512 → Option B for PR2+ | **Met** — adopt Option B |
| Basic ≤512 but advanced >512 → split Option A PR2 / Option B PR3 | Partially met (2–3 ≤512, 5 = 2617) but scenario 4 (basic) also >512 → **single Option B for PR2 and PR3** |

### Why Option B (not Option A)

| Criterion | Option A (inline JSON) | Option B (JSONB + compact ref + hydration) |
|-----------|------------------------|--------------------------------------------|
| Min/typical basic (scenarios 2–3) | 293–414 chars — fits | Compact ref 22–72 chars |
| Full basic UI (scenario 4) | **881 chars — fails** | Hydrated from JSONB |
| Max advanced (scenario 5) | **2,617 chars — fails** | Hydrated from JSONB |
| FusionX basic UI parity (5 groups) | Requires dropping groups | **Full parity** |
| Agent compatibility | High — inline JSON | **High** — hydration restores full JSON on poll |
| `GetPendingTasksAsync` | No changes | `WindowsPowerManagementTaskPayloadHydrator` |
| Consistency | Keyboard / Computer Name | **802.1X / Wireless Properties (ADR-0001 / ADR-0003)** |

**Hydration required** — `AgentTaskService.GetPendingTasksAsync` must expand compact `FunctionParameter` into full `{"WinCELinux":{"XPPowerManagement":{…}}}` before returning tasks to the agent.

### PR1+ task contract

| Field | Value |
|-------|-------|
| `ModuleName` | `Power Management Settings` |
| `FunctionName` | `Now` / `Update` (Intellinode; FusionX ExecuteNow / ExecuteLater) |
| `ExtraData` | `{macAddress}&PMO,{planName}` (FusionX handler default; confirm comma suffix with agent team) |
| `FunctionParameter` (stored in DB) | `{"settingsVersion":<N>}` — **22 chars**; optional `"planName":"Balanced"` if multi-plan pending tasks need disambiguation |
| Full agent JSON (API response) | Hydrated at `GetPendingTasksAsync` from `device_windows_power_management_settings.settings_json` |
| `SettingsKind` | `WindowsPowerManagement` (PR1 enum) |
| Settings storage | `device_windows_power_management_settings` — JSONB per device + plan + version/apply columns |
| Hydration | `WindowsPowerManagementTaskPayloadHydrator` (reuse `Windows8021xTaskPayloadHydrator` pattern) |
| OS v1 | `:XP` only |
| Schedule `ModuleName` (FusionX parity) | Power plan name (`Balanced`, `High performance`, …) on schedule row — distinct from module type string |

## Payload size measurements

Measured with `System.Text.Json` (default naming), spike test `WindowsPowerManagementPayloadSizeSpikeTests` in `tests/Intellinode.Infrastructure.Tests/WindowsPowerManagement/`.

| Scenario | Serialized size (chars) | Fits 512? |
|----------|-------------------------|-----------|
| Keyboard comparable (`XPKeyboard`) | 129 | Yes |
| Min basic power (Balanced, Display only) | 293 | Yes |
| Typical basic (Balanced, Display + Sleep) | 414 | Yes |
| Full basic UI (5 FusionX option groups) | **881** | **No (+369 over)** |
| Max advanced (all groups, longest FusionX catalog strings) | **2,617** | **No (~5.1× over)** |
| Option B compact reference `{"settingsVersion":42}` | 22 | Yes |
| Option B compact reference `{"settingsVersion":42,"deviceId":"<uuid>"}` | 72 | Yes |

Typical basic sample (abbreviated):

```json
{
  "WinCELinux": {
    "XPPowerManagement": {
      "strPowerSchemaName": "Balanced",
      "blIsActive": true,
      "objPowerOptions": [
        {
          "strPowerOptionName": "Display",
          "objPowerSettings": [
            { "strSettingName": "Turn off display after", "strSettingValue": "10 minutes" }
          ]
        },
        {
          "strPowerOptionName": "Sleep",
          "objPowerSettings": [
            { "strSettingName": "Sleep after", "strSettingValue": "30 minutes" }
          ]
        }
      ],
      "Operation": "Update",
      "Index": "1",
      "TaskID": 42,
      "AgentAction": 0
    }
  }
}
```

## FusionX parity

| Field | FusionX | Intellinode (proposed) |
|-------|---------|------------------------|
| UI area | System Settings → Power Management | Same scope |
| Module type (`ModuleType`) | `Power Management Settings` | `Power Management Settings` |
| Schedule `ModuleName` | Power plan name (`Balanced`, …) | Same (plan name on schedule metadata) |
| Function names | Execute-now / queue via Task Manager | `Now` / `Update` |
| Signal (`ExtraData`) | `{mac}&PMO,{planName}` | `{mac}&PMO,{planName}` (pending agent confirmation) |
| Agent action parameter | `PowerManagementAgentAction` → `@AgentAction` on task proc | Map to `AgentAction` field in hydrated JSON |
| Wrapper key | `XPPowerManagement` | `XPPowerManagement` |
| Payload wire format (FusionX) | Binary `FunctionObject` blob (`SerializeObject`) | JSON `{"WinCELinux":{"XPPowerManagement":{…}}}` |
| Payload storage | SQL plan tables + blob | JSONB `settings_json` + compact version reference on task |
| Agent delivery | Fetch/deserialize blob at poll | Hydrate JSON at poll from settings row by `settingsVersion` |
| Basic option groups | Display, Hard disk, Sleep, Power buttons and lid, System standby | Same five groups (PR2) |
| Advanced options | `XP_PowerPlan_AdvanceDetails` XML tree | JSONB or XML snapshot (PR3) |
| Shutdown/reboot tasks | `Power Management` module (separate) | Out of scope — existing generic task API |

## Consequences

### Positive

- Honors full FusionX basic UI (5 option groups) and advanced trees without widening `device_tasks` for all modules.
- Preserves agent polling contract (`functionParameter` still contains apply-ready JSON after hydration).
- Reuses proven 802.1X / Wireless Properties hydration pattern.
- Clear separation from generic `Power Management` shutdown tasks.

### Negative

- Diverges from Keyboard / Computer Name inline simplicity.
- `GetPendingTasksAsync` gains another module-specific hydrator.
- Plan-scoped pending tasks may require `planName` in compact reference or per-plan settings rows.
- FusionX minute-value stripping vs display strings must be handled consistently in payload builder.

### Risks

- **Signal format** — `{mac}&PMO,{planName}` comma suffix is unusual vs other modules (`&SCR`, `&WNS`); wrong format breaks agent routing.
- **JSON wrapper vs unwrapped struct** — FusionX AppService sometimes serializes `XPPowerManagement` without `WinCELinux` wrapper; confirm agent poll handler accepts Intellinode wrapper.
- **Race** — settings updated after task queued but before agent polls — bind task to `settingsVersion` at queue time (same mitigation as ADR-0001).
- **Basic vs advanced split** — PR2 basic and PR3 advanced may share one JSONB row or separate advance snapshot; PR1 schema must allow PR3 expansion.
- **Option A false hope** — min/typical basic fit 512, but product requires full 5-group basic UI in PR2.

## Follow-up PRs

- **PR1**: Entity `DeviceWindowsPowerManagementSettings`, migration (`settings_json` JSONB), `SettingsKind.WindowsPowerManagement`, power plan reference stubs, payload builder + `IWindowsPowerManagementTaskPayloadHydrator` interface (no controller).
- **PR2**: Basic apply service (5 option groups), compact `FunctionParameter`, hydration, ack handler, pending-task blocking per module.
- **PR3**: Advanced option tree apply (`XP_PowerPlan_AdvanceDetails` parity), extended JSONB, controller + HTTP samples.
- **PR4**: Template / SysView queue parity (optional).

## Appendix: Open questions for agent team

Do not block PR1 design; confirm before PR3 agent integration:

1. **JSON wrapper** — Does the agent expect `{"WinCELinux":{"XPPowerManagement":{…}}}` or unwrapped `XPPowerManagement` JSON (FusionX AppService encrypt path)?
2. **Binary vs JSON** — Legacy tasks store binary `FunctionObject`; confirm JSON field names match struct exactly (`strPowerSchemaName`, `objPowerOptions`, …).
3. **Signal suffix** — Is `{mac}&PMO,{planName}` required, or `{mac}&PMO` only?
4. **Setting values** — Does agent expect `"10 minutes"`, `"10 Minutes"`, or numeric `"10"` after DAC-style stripping?
5. **Basic vs advanced task split** — One task applies full struct (basic + advanced) or separate tasks per UI surface?
6. **`ModuleName` on task row** — FusionX schedule uses plan name in `ModuleName`; Intellinode task row uses module type string — confirm agent routing uses module type + signal, not plan name alone.
7. **Generic `Power Management` shutdown tasks** — Confirm no collision with `Power Management Settings` handler registration.

---

**Ready for PR1: Yes**

Blockers: none for PR1 design. PR1 should include optional `planName` in compact task reference if multiple plans per device need concurrent pending tasks. Confirm signal suffix and JSON wrapper with agent team before PR3. Re-spike if PR3 advance XML normalization produces payloads larger than JSONB comfort (unlikely vs 802.1X).
