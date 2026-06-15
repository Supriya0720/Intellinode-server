# ADR-0006: Windows Wallpaper Agent Payload Strategy

## Status

Accepted

## Context

Intellinode is adding REST APIs for **Windows Wallpaper** (FusionX **User Settings → User Interface Settings → Wallpaper Settings**; Windows `:XP` only in v1), mirroring the **Screen Saver** admin API and task pipeline. PR0 required a payload size spike **before PR1** to measure realistic agent JSON sizes and choose between inline JSON and server-side hydration.

Wallpaper payloads use FusionX struct `WinCELinux.XPWallPaper` (`structXP_Data.cs` ~L4107). The struct is nearly identical to `XPScreenSaver` in shape: core browse path is compact; repository / FTP upload metadata can exceed the **`device_tasks.function_parameter` 512-char limit**.

### FusionX Wallpaper delivery (parity target)

FusionX **User Settings → Wallpaper** (`UCWindowsWallpaper.ascx`, `WindowsWallpaperHandler.ashx.cs`, `WindowsWallpaperDAC.cs`):

1. **`WindowsWallpaperDAC.UpdateToDatabase`**
   - Maps UI → `WinCELinux.XPWallPaper`.
   - Serializes with `clsCommon.SerializeObject` → binary `byte[]` (`FunctionObject` blob).
   - Queues via `prc_TaskManager_ExecuteNow_NEW` / bulk paths.

2. **Schedule / task metadata (`setWallpaperObjjectsFromUI`)**
   - `objSchedule.ModuleType` = **`"Wallpaper"`**
   - `objSchedule.ModuleName` = `string.Empty`
   - `objSchedule.Signal` = `{macAddress}&WPS`
   - `RepositoryType` / UI `Source` = `Browse`, `Upload`, or `Repository`
   - `strText1` = prevent-user wallpaper flag
   - `strText2` = domain for repository

3. **Agent struct** (`structXP_Data.cs` ~L4107):

```csharp
public struct XPWallPaper {
    public bool blUpload;
    public string DownloadIP;
    public string ProtocolType;
    public string FTPFolderPath;
    public string FTPpassword;
    public string FTPSSLType;
    public string FTPUsername;
    public int Port;
    public string RepositoryType;
    public string strPictureName;
    public string strPicturePosition;
    public int TaskID;
    public int AgentAction;
    public string strText1;  // prevent user changes
    public string strText2;  // domain for repository
    public string strText3;
    public string strText4;
    public string strText5;
    public int ConnectionId;
    public int LoggedInUserID;
    public string ConnectionName;
}
```

4. **Intellinode wire format** (JSON target):

```json
{
  "WinCELinux": {
    "XPWallPaper": {
      "blUpload": false,
      "strPictureName": "C:\\Wallpapers\\corp.jpg",
      "strPicturePosition": "Stretch",
      "RepositoryType": "Browse",
      "strText1": "true",
      "TaskID": 42,
      "AgentAction": 0
    }
  }
}
```

## Decision

**Adopt the same split strategy as [ADR-0005](./0005-windows-screen-saver-payload-strategy.md):**

| Phase | Path | Strategy |
|-------|------|----------|
| **PR2 (MVP)** | Core browse settings (path, position, prevent-user) | **Option A** — inline full JSON in `device_tasks.function_parameter` |
| **PR3 (repository/upload)** | FTP / repository delivery (`blUpload = true`) | **Option B** — `repository_json` JSONB + compact `{"settingsVersion":N}` + `WindowsWallpaperTaskPayloadHydrator` at poll time |

### Spike decision rules applied

| Rule | Result |
|------|--------|
| Core browse scenarios (typical paths) ≤512 → Option A for PR2 | **Met** |
| Extreme browse path >512 → PR2 validates `picturePath` max length | **Met** |
| Repository scenario >512 → Option B for PR3 | **Met** |
| All scenarios ≤512 → single Option A | **Not met** |

## Payload size measurements

Measured with `System.Text.Json` (default naming), spike test `WindowsWallpaperPayloadSizeSpikeTests` in `tests/Intellinode.Infrastructure.Tests/Wallpaper/`. Builder: `WindowsWallpaperPayloadBuilder`.

| Scenario | Fits 512? |
|----------|-----------|
| Browse min core | Yes |
| Browse typical (prevent-user, Center) | Yes |
| Browse long path (~130 chars) | Yes |
| Browse extreme path (>200 char segment) | **No** — PR2 will cap `picturePath` length |
| Repository max FTP metadata | **No** |
| Option B compact reference `{"settingsVersion":42}` | Yes (22 chars) |

## PR1+ task contract (planned)

| Field | Value |
|-------|-------|
| `ModuleName` | `Wallpaper` |
| `FunctionName` | `Now` / `Update` |
| `ExtraData` | `{macAddress}&WPS` |
| `FunctionParameter` (PR2) | Full inline JSON (≤ 512 chars) |
| `FunctionParameter` (PR3 upload) | `{"settingsVersion":<N>}` |
| `SettingsKind` | `WindowsWallpaper` |
| Settings storage | `device_windows_wallpaper_settings` |
| Hydration (PR3 only) | `WindowsWallpaperTaskPayloadHydrator` |
| OS v1 | `:XP` only |

Constants: `WindowsWallpaperModuleConstants` in `WindowsWallpaperContracts.cs`.

## Consequences

- PR1 delivers read-only GET API and persistence only.
- PR2 can ship browse applies without hydrator changes to `AgentTaskService`.
- PR3 adds snapshot table, hydrator decorator, and repository validation (reuse `WindowsWallpaperRequestValidation`).

## References

- FusionX: `WindowsWallpaperDAC.cs`, `UCWindowsWallpaper.ascx.cs`, `WindowsWallpaperHandler.ashx.cs`
- Intellinode precedent: [ADR-0005](./0005-windows-screen-saver-payload-strategy.md)
- Spike tests: `tests/Intellinode.Infrastructure.Tests/Wallpaper/WindowsWallpaperPayloadSizeSpikeTests.cs`
