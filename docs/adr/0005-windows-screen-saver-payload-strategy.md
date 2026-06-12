# ADR-0005: Windows Screen Saver Agent Payload Strategy

## Status

Accepted

## Context

Intellinode is adding REST APIs for **Windows Screen Saver** (FusionX **User Settings → User Interface Settings → Screen Saver Settings**; Windows `:XP` only in v1), mirroring the **Keyboard Settings** admin API and task pipeline. PR0 required a payload size spike **before PR1** to measure realistic agent JSON sizes and choose between inline JSON (Keyboard / Computer Name parity) and server-side hydration (802.1X / Power Management parity).

Screen saver payloads use FusionX struct `WinCELinux.XPScreenSaver` (`structXP_Data.cs`). Unlike Power Management (881+ chars for full basic UI — see [ADR-0004](./0004-windows-power-management-payload-strategy.md)), the **core browse path** (screensaver name, timeout, password flag, prevent-user flag) is structurally small. The **repository / FTP upload path** adds many string fields (connection metadata, folder paths, credentials) and can exceed the **`device_tasks.function_parameter` 512-char limit**.

### Current Keyboard task flow (Intellinode reference)

1. **`KeyboardSettingsService.QueueKeyboardWorkAsync`**
   - Upserts `device_keyboard_settings`.
   - Builds `{"WinCELinux":{"XPKeyboard":{...}}}` inline.
   - Validates `functionPayload.Length <= 512`.
   - Creates `device_tasks` with full JSON in `FunctionParameter`.

2. **`AgentTaskService.GetPendingTasksAsync`**
   - Returns stored `FunctionParameter` verbatim — **no hydration**.

Typical Keyboard `functionParameter` size: **~129 chars**.

### Database constraints

| Column | Max length | Source |
|--------|------------|--------|
| `device_tasks.function_parameter` | **512** | `IntellinodeDbContext` → `HasMaxLength(512)` |
| `device_tasks.extra_data` | **512** | Same |
| `AgentValidators` (admin queue) | **512** | `FunctionParameter` rule |

### FusionX Screen Saver delivery (parity target)

FusionX **User Settings → Screen Saver** (`UCWindowsScreenSaver.ascx`, `ScreenSaverHandler.ashx.cs`, `WindowsScreenSaverDAC.cs`):

1. **`WindowsScreenSaverDAC.UpdateToDatabase`**
   - Maps UI → `WinCELinux.XPScreenSaver` (`blScreenSaverPasswordProtected`, `intScreenSaverTimeOut`, `strCurrentScreenSaver`, upload/FTP fields, `strText1` = prevent-user flag).
   - Serializes with `clsCommon.SerializeObject` → **binary `byte[]`** (`FunctionObject` blob).
   - Queues via `prc_TaskManager_ExecuteNow_NEW` / bulk `TaskManager_ExecuteNow_Bulk_withConfigParams`.

2. **Schedule / task metadata (`SetScreenSaverSettingsfromGUI`)**
   - `objSchedule.ModuleType` = `SetScreenSaverAsModuleTypeMUI` → **`"ScreenSaver"`**
   - `objSchedule.ModuleName` = `string.Empty`
   - `objSchedule.Signal` = `{macAddress}&SCR` (`clsCommon.strScreenSaver`)
   - `Parameter` = agent action (`"0"` default; `"Connection"` during repository validation)

3. **Load path (`ScreenSaverLoad`)**
   - `Input_prcGetScreenSaverList` — device-reported `.scr` names
   - `Input_prcGetScreenSaver` — current timeout, password flag, active saver

4. **Agent struct** (`structXP_Data.cs` ~L2328):

```csharp
public struct XPScreenSaver {
    public int intScreenSaverTimeOut;
    public bool blScreenSaverPasswordProtected;
    public string strCurrentScreenSaver;
    public bool blUpload;
    public int ConnectionId;
    public string DownloadIP;
    public string FTPFolderPath;
    public string FTPpassword;
    public string FTPSSLType;
    public string FTPUsername;
    public int LoggedInUserID;
    public int Port;
    public string ProtocolType;
    public string RepositoryType;
    public string strText1;  // prevent user changes
    public string strText2;  // domain for repository
    public string strText3;
    public string strText4;
    public string strText5;
    public int TaskID;
    public int AgentAction;
    public string ConnectionName;
}
```

5. **FusionX wire format** — binary `FunctionObject` blob. Intellinode target:

```json
{
  "WinCELinux": {
    "XPScreenSaver": {
      "intScreenSaverTimeOut": 15,
      "blScreenSaverPasswordProtected": true,
      "strCurrentScreenSaver": "Bubbles.scr",
      "blUpload": false,
      "RepositoryType": "Browse",
      "strText1": "true",
      "TaskID": 42,
      "AgentAction": 0
    }
  }
}
```

**Contrast with Power Management:** core screen saver fits 512; full repository metadata does not. **Contrast with Keyboard:** same wrapper pattern; screen saver has more scalar fields but still compact on browse path.

### Separation: Screen Saver settings vs Screen Saver logs

| Concern | FusionX module | Intellinode scope |
|---------|----------------|-------------------|
| **Screen saver configuration** | `ScreenSaver` / `XPScreenSaver` | PR1–PR3 (this ADR) |
| **Screen saver event logs** | `ScreenSaver_Logs_Handler`, FDM reports | Out of scope — separate module; `ScreensaverLogsEnabled` already exists on agent advanced settings |

## Decision

**Adopt a split strategy:**

| Phase | Path | Strategy |
|-------|------|----------|
| **PR2 (MVP)** | Core browse settings (name, timeout, password, prevent-user) | **Option A** — inline full JSON in `device_tasks.function_parameter` (Keyboard parity) |
| **PR3 (repository/upload)** | FTP / repository delivery (`blUpload = true`, connection metadata) | **Option B** — `settings_json` JSONB + compact `{"settingsVersion":N}` + `WindowsScreenSaverTaskPayloadHydrator` at poll time |

Fallback for PR3: **field length caps** to keep inline JSON ≤512 — **rejected** because FusionX validates live FTP paths and repository connections without tight caps; hydration matches [ADR-0001](./0001-windows-802-1x-payload-strategy.md) / [ADR-0004](./0004-windows-power-management-payload-strategy.md) precedent.

### Spike decision rules applied

| Rule | Result |
|------|--------|
| Core scenarios (min, typical, long name) all ≤512 → Option A for PR2 | **Met** — 431–497 chars |
| Repository scenario >512 → Option B for PR3 | **Met** — max repository >512 |
| All scenarios ≤512 → single Option A | **Not met** |

### Why Option A for PR2 (not Option B everywhere)

| Criterion | Option A (inline JSON) | Option B (JSONB + hydration) |
|-----------|------------------------|------------------------------|
| Core browse payloads | **431–497 chars — fits** | Compact ref 22 chars — unnecessary |
| Agent compatibility | High — same as Keyboard | High — requires hydrator |
| `GetPendingTasksAsync` | **No changes** | Module-specific hydrator |
| PR2 delivery speed | **Minimal diff** | Extra table + hydrator for simple scalars |

### Why Option B for PR3 repository (not capped inline)

| Criterion | Capped inline (Option A) | Option B (hydration) |
|-----------|--------------------------|----------------------|
| Full FTP/repository metadata | Requires rejecting long FusionX-valid paths | **Full parity** |
| Consistency | Unique per-module cap rules | **802.1X / Power Management pattern** |
| Race safety | N/A | Bind task to `settingsVersion` at queue time |

### PR1+ task contract (PR2 core)

| Field | Value |
|-------|-------|
| `ModuleName` | `ScreenSaver` |
| `FunctionName` | `Now` / `Update` |
| `ExtraData` | `{macAddress}&SCR` |
| `FunctionParameter` (PR2) | Full inline JSON (≤ 512 chars) |
| `FunctionParameter` (PR3 upload) | `{"settingsVersion":<N>}` — compact reference |
| `SettingsKind` | `WindowsScreenSaver` (PR1 enum) |
| Settings storage | `device_windows_screen_saver_settings` (PR1) |
| Hydration (PR3 only) | `WindowsScreenSaverTaskPayloadHydrator` |
| OS v1 | `:XP` only |

Constants: `WindowsScreenSaverModuleConstants` in `WindowsScreenSaverContracts.cs`.

## Payload size measurements

Measured with `System.Text.Json` (default naming), spike test `WindowsScreenSaverPayloadSizeSpikeTests` in `tests/Intellinode.Infrastructure.Tests/ScreenSaver/`. Builder: `WindowsScreenSaverPayloadBuilder`.

| Scenario | Serialized size (chars) | Fits 512? |
|----------|-------------------------|-----------|
| Keyboard comparable (`XPKeyboard`) | 129 | Yes |
| Min core (None, browse, no upload) | **431** | Yes |
| Typical core (Bubbles.scr, password, prevent-user) | **436** | Yes |
| Long screensaver name (64-char + `.scr`) | **497** | Yes |
| Max repository / FTP metadata | **>512** (spike asserts exceed) | **No** |
| Option B compact reference `{"settingsVersion":42}` | **22** | Yes |

Typical core sample (abbreviated):

```json
{
  "WinCELinux": {
    "XPScreenSaver": {
      "intScreenSaverTimeOut": 15,
      "blScreenSaverPasswordProtected": true,
      "strCurrentScreenSaver": "Bubbles.scr",
      "blUpload": false,
      "RepositoryType": "Browse",
      "strText1": "true",
      "TaskID": 42,
      "AgentAction": 0
    }
  }
}
```

## FusionX parity

| Field | FusionX | Intellinode (proposed) |
|-------|---------|------------------------|
| UI area | User Settings → Screen Saver | Same scope |
| Module type (`ModuleType`) | `ScreenSaver` | `ScreenSaver` |
| Schedule `ModuleName` | `string.Empty` | `string.Empty` |
| Function names | Execute-now / queue via Task Manager | `Now` / `Update` |
| Signal (`ExtraData`) | `{mac}&SCR` | `{mac}&SCR` |
| Wrapper key | `XPScreenSaver` | `XPScreenSaver` |
| Payload wire format (FusionX) | Binary `FunctionObject` blob | JSON `{"WinCELinux":{"XPScreenSaver":{…}}}` |
| Payload storage (PR2) | SQL + blob | Scalar columns + inline task JSON |
| Payload storage (PR3 upload) | SQL + blob | JSONB `repository_json` + compact version ref |
| Agent delivery | Fetch/deserialize blob at poll | Inline JSON (PR2) or hydrate (PR3) |
| Screensaver list | `Input_prcGetScreenSaverList` | `GET .../catalog` (PR1 stub / agent inventory) |
| Prevent user changes | `strText1` / `chkPreventUserScreenSaver` | `PreventUserChanges` → `strText1` |
| Password | Boolean flag only (not password string) | Same |
| Linux screen saver | `Lx_ScreenSaver` / `Linux_Screensaver` table | **Deferred** — separate `SettingsKind` track |

## Consequences

### Positive

- PR2 ships quickly with Keyboard-parity pipeline (no hydrator for MVP).
- Core admin scenarios (typical + long built-in name) fit 512 with headroom (~15 chars on long name).
- PR3 repository path has a clear upgrade path without blocking PR1/PR2 schema design (`repository_json` nullable JSONB).
- Signal `SCR` is unambiguous (distinct from Keyboard/Mouse/Display config suffix reuse in `appsettings.json`).

### Negative

- Two delivery modes (inline vs hydrated) in one module — service must branch on `blUpload` / `SourceType`.
- PR3 adds `AgentTaskService` hydrator registration.
- Screensaver catalog requires agent/device inventory integration (FusionX reads live from device).

### Risks

- **JSON wrapper** — Confirm agent accepts `{"WinCELinux":{"XPScreenSaver":{…}}}` vs unwrapped struct.
- **Binary vs JSON field names** — Must match struct exactly (`FTPpassword` casing, `LoggedInUserID`, etc.).
- **SCR signal collision** — Keyboard/Mouse/Display `appsettings.json` use `SCR` as `DefaultSignalSuffix`; screen saver module routing uses `ModuleName` = `ScreenSaver` — confirm agent disambiguates by module name + signal.
- **Race (PR3)** — Settings updated after queue, before poll — bind `settingsVersion` at queue time.
- **Linux deferred** — `Lx_ScreenSaver` is a different schema; do not reuse Windows table/API.

## Follow-up PRs

- **PR1**: `SettingsKind.WindowsScreenSaver`, `device_windows_screen_saver_settings`, `WindowsScreenSaverPayloadBuilder`, module constants, read-only `GET` API, optional catalog stub.
- **PR2**: Core apply service (browse path, Option A inline JSON), ack handler, validators (payload ≤512), controller + HTTP samples.
- **PR3**: Repository/upload apply (Option B hydration), `WindowsScreenSaverTaskPayloadHydrator`, `repository_json` JSONB column.
- **PR4**: Group bulk / template queue parity (optional).
- **PR5**: Linux `Lx_ScreenSaver` track (separate enum/API).
- **PR6**: Screen saver logs reporting (optional).

## Appendix: Open questions for agent team

Do not block PR1 design; confirm before PR2 agent integration:

1. **JSON wrapper** — `{"WinCELinux":{"XPScreenSaver":{…}}}` or unwrapped?
2. **Field casing** — Confirm `FTPpassword`, `LoggedInUserID`, `DownloadIP` match agent deserializer.
3. **`strText1`** — `"true"` / `"false"` strings (FusionX GUI) vs JSON boolean?
4. **`(None)` screensaver** — Literal string for disabled saver?
5. **Catalog source** — Does agent return screensaver list via inventory poll or only on dedicated module read?
6. **SCR signal** — Same suffix as other user-settings modules; is `ModuleName` = `ScreenSaver` sufficient for routing?

---

**Ready for PR1: Yes**

Blockers: none for PR1 design. PR1 schema should include nullable `repository_json` JSONB for PR3. PR2 uses Option A inline JSON; PR3 adds hydrator without changing PR2 task rows.
