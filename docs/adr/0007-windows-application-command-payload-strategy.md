# ADR-0007: Windows Application Command Agent Payload Strategy

## Status

Accepted

## Context

Intellinode is adding REST APIs for **Windows Application Command** (FusionX **Administration → Application command**; Windows `:XP` only in v1), mirroring the **Wallpaper / Screen Saver** admin API and task pipeline. PR0 required a payload size spike **before PR1** to measure realistic agent JSON sizes and choose between inline JSON and server-side hydration.

Application Command has **two apply modes** on one admin screen:

| UI mode | FusionX `ModuleType` | Agent struct |
|---------|---------------------|--------------|
| Remote Application | `"Application"` | `WinCELinux.Application` |
| Remote Command | `"Command"` | `WinCELinux.Command` |

Unlike Wallpaper, there is **no repository/upload path** — payloads are scalar strings and flags only. The spike determines whether all v1 scenarios fit the existing **`device_tasks.function_parameter` 512-char limit** without hydration.

### Database constraints

| Column | Max length | Source |
|--------|------------|--------|
| `device_tasks.function_parameter` | **512** | `IntellinodeDbContext` → `HasMaxLength(512)` |
| `device_tasks.extra_data` | **512** | Same |
| `AgentValidators` (admin queue) | **512** | `FunctionParameter` rule |

### FusionX Application Command delivery (parity target)

FusionX **Administration → Application command** (`Windows_Application_And_Command_Handler.ashx.cs`, `WindowsApplicationpathDAC.cs`, `ModulesWindows.js`):

1. **`WindowsApplicationpathDAC.UpdateToDatabase`**
   - Maps UI → `WinCELinux.Application` or `WinCELinux.Command` depending on `IsApplicationOrCommand`.
   - Serializes with `clsCommon.SerializeObject` → binary `byte[]` (`FunctionObject` blob).
   - Queues via `prc_TaskManager_ExecuteNow_NEW` / `ExecuteLater`.

2. **Schedule / task metadata (`SetDataFromGUI`)**
   - Application: `objSchedule.ModuleType` = **`"Application"`**
   - Command: `objSchedule.ModuleType` = **`"Command"`**
   - `objSchedule.ModuleName` = `string.Empty`
   - `objSchedule.Operation` = `Update`
   - Instant vs queue: `InstantApply` / `ScheduleApply` / `QueueTemplate`
   - `Text1` = reboot required (`"0"` / `"1"`)
   - Command `Text2` = require command output (`RequiredCommandOutput`)

3. **Signal (`ExtraData`)**
   - Single-device handler path: Signal often **not set** (empty).
   - Bulk business path: `{macAddress}&196&Insert&` / `{macAddress}&196&Edit&{ModuleName}` (ModuleName empty in practice).
   - Intellinode default suffix: **`196`** (configurable via `WindowsApplicationCommandOptions` in PR1).

4. **Agent structs** (`structXP_Data.cs` ~L4871):

```csharp
public struct Application {
    public string ApplicationPath { get; set; }
    public string Parameter { get; set; }
    public bool IsWarnUser { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public string MessageType { get; set; }
    public string DisplayTime { get; set; }
    public string Text1 { get; set; }  // reboot required
    public string Text2 { get; set; }
    // Text3–Text5, TaskID, AgentAction
}

public struct Command {
    public string strCommand { get; set; }
    public string TimeOut { get; set; }
    public string Text1 { get; set; }  // reboot required
    public string Text2 { get; set; }  // require output
    // Text3–Text5, TaskID, AgentAction
}
```

5. **Intellinode wire format** (JSON target — struct names match FusionX binary type names, not `XP*` prefix):

```json
{
  "WinCELinux": {
    "Application": {
      "ApplicationPath": "C:\\Program Files\\App\\app.exe",
      "Parameter": "/silent",
      "IsWarnUser": true,
      "Title": "Notice",
      "Message": "App will launch",
      "MessageType": "1",
      "DisplayTime": "30",
      "Text1": "0",
      "TaskID": 42,
      "AgentAction": 0
    }
  }
}
```

```json
{
  "WinCELinux": {
    "Command": {
      "strCommand": "C:\\Windows\\System32\\cmd.exe /c dir",
      "TimeOut": "60",
      "Text1": "0",
      "Text2": "1",
      "TaskID": 42,
      "AgentAction": 0
    }
  }
}
```

### FusionX UI field mapping

| FusionX UI / JS | Application struct | Command struct |
|-----------------|-------------------|----------------|
| `txtAppsPath` | `ApplicationPath` | — |
| `txtParameters` | `Parameter` | — |
| `chbxWarnUser` | `IsWarnUser` | — |
| `txtTitle`, `txtMsg`, `ddlMsgType`, `ddlDisplayTime` | alert fields | — |
| `txtCommand` | — | `strCommand` |
| `ddlTimeOut` | — | `TimeOut` |
| `idRebootRequired` | `Text1` | `Text1` |
| `RequiredCommandOutput` | — | `Text2` |

## Decision

**Adopt Option A — inline full JSON in `device_tasks.function_parameter` for all v1 scenarios** (Keyboard / Wallpaper browse parity). **No hydration** (Option B) is required for Application Command.

| Phase | Path | Strategy |
|-------|------|----------|
| **PR2 (MVP)** | Application + Command instant/queue apply | **Option A** — inline JSON (≤ 512 chars) |
| **PR3+** | Reference dropdowns, command denylist | Validation only — still inline JSON |

### Spike decision rules applied

| Rule | Result |
|------|--------|
| Typical Application + Command scenarios ≤512 | **Met** (~250–350 chars) |
| Worst-case at PR2 field caps ≤512 | **Met** (512 chars at cap) |
| Any scenario >512 without caps | **Met** — extreme paths/messages exceed 512; PR2 validates field caps |
| Repository/upload path needing hydration | **N/A** — not in scope |

### PR2 field length caps (spike-derived)

Enforced via `WindowsApplicationCommandModuleConstants` and `WindowsApplicationCommandRequestValidation`:

| Field | Max length |
|-------|------------|
| `applicationPath` | 120 |
| `parameters` | 32 |
| `alertTitle` | 32 |
| `alertMessage` | 87 |
| `messageType` | 4 |
| `displayTime` | 4 |
| `commandText` | 200 |
| `timeout` | 4 |

FusionX UI validates Application path as `X:\...\*.exe` (see `ApplySettings` in `ModulesWindows.js`).

### PR1+ task contract (planned)

| Field | Application mode | Command mode |
|-------|------------------|--------------|
| `ModuleName` | `Application` | `Command` |
| `FunctionName` | `Now` / `Update` / `QueueTemplate` | same |
| `ExtraData` | `{macAddress}&196` (default) | same |
| `FunctionParameter` | Inline JSON (≤ 512) | Inline JSON (≤ 512) |
| `SettingsKind` | `WindowsApplication` | `WindowsCommand` |
| Settings storage | `device_windows_application_command_settings` (single table, `mode` column) | same row shape |
| OS v1 | `:XP` only | `:XP` only |

Constants: `WindowsApplicationCommandModuleConstants` in `WindowsApplicationCommandContracts.cs`.

## Payload size measurements

Measured with `System.Text.Json` (default naming), spike test `WindowsApplicationCommandPayloadSizeSpikeTests` in `tests/Intellinode.Infrastructure.Tests/ApplicationCommand/`. Builder: `WindowsApplicationCommandPayloadBuilder`.

| Scenario | Serialized size (chars) | Fits 512? |
|----------|-------------------------|-----------|
| Application min (notepad, no alert) | ~250 | Yes |
| Application typical (alert + parameters) | ~343 | Yes |
| Application max at PR2 caps | **512** | Yes |
| Command typical (timeout + output flag) | ~220 | Yes |
| Command max at PR2 caps | ~280 | Yes |
| Extreme path/message (uncapped) | **>512** | No — rejected by PR2 validation |

## FusionX parity

| Field | FusionX | Intellinode (proposed) |
|-------|---------|------------------------|
| UI area | Administration → Application command | Same scope |
| Module type | `Application` / `Command` | Same (mode-dependent) |
| Schedule `ModuleName` | `string.Empty` | `string.Empty` |
| Function names | Execute-now / queue via Task Manager | `Now` / `Update` / `QueueTemplate` |
| Signal | `{mac}&196&…` (bulk) or empty (single) | `{mac}&196` default |
| Wrapper keys | Binary `Application` / `Command` | JSON `Application` / `Command` under `WinCELinux` |
| Payload wire format (FusionX) | Binary blob | JSON inline |
| Apply guards | Pending/in-process, FBWF, license | Pending/in-process + enrollment (PR2); FBWF deferred |
| Group scheduled profile | `SaveCitrixReceiverSettings` | Device settings table (PR1); group bulk PR4 |

## Consequences

### Positive

- Single Option A pipeline — no hydrator, no snapshot table for v1.
- Typical admin scenarios fit 512 with comfortable headroom.
- Dual mode handled by one payload builder with mode switch.
- Reuses Wallpaper/Screen Saver controller and service patterns.

### Negative

- Alert message and application path caps are tighter than Windows MAX_PATH; document in API.
- Two `SettingsKind` values for one UI (matches FusionX separate module types).
- Signal parity is approximate (`196` suffix vs FusionX bulk `196&Insert&`).

### Risks

- **JSON wrapper keys** — Agent must accept `Application` / `Command` (not `XPApplication`). Confirm before PR2 agent integration.
- **Text1/Text2** — FusionX uses `"0"`/`"1"` strings; Intellinode serializes same.
- **Command security** — No FusionX shutdown block in active code; PR3 may add denylist.
- **FBWF guard** — FusionX blocks group apply when FBWF enabled; defer until Intellinode has FBWF state.

## Follow-up PRs

- **PR1**: `SettingsKind.WindowsApplication` + `WindowsCommand`, `device_windows_application_command_settings`, read-only GET API, options.
- **PR2**: Apply service, ack handlers (mode-aware), validators, controller + HTTP samples.
- **PR3**: Reference dropdowns (message type, display time, timeout), optional command denylist.
- **PR4**: Bulk / group / template queue parity (optional).

## Appendix: Open questions for agent team

Do not block PR1 design; confirm before PR2 agent integration:

1. **JSON wrapper keys** — `WinCELinux.Application` / `WinCELinux.Command` vs `XP*` prefixed names?
2. **Signal suffix** — Is `{mac}&196` sufficient, or must `ExtraData` include `&Insert&` / `&Edit&`?
3. **`IsWarnUser` false** — Are empty Title/Message/MessageType/DisplayTime acceptable?
4. **Command output** — Does agent honor `Text2` = `"1"` for stdout capture?

---

**Ready for PR1: Yes**

Blockers: none for PR1 design. PR2 uses Option A inline JSON with field caps above.
