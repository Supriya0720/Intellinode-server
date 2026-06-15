# Windows Wallpaper — Overview

## 1. Overview

Intellinode is porting FusionX **User Settings → User Interface Settings → Wallpaper Settings** to a modern ASP.NET API. v1 targets **Windows `:XP` devices only**.

PR0 ([ADR-0006](./adr/0006-windows-wallpaper-payload-strategy.md)) completed payload spike and split-strategy decision: **Option A inline JSON for PR2 core**, **Option B hydration for PR3 repository/upload**.

---

## 2. Module map (FusionX UI → Intellinode PRs)

| FusionX UI area | FusionX module / struct | Intellinode PR |
|-----------------|-------------------------|----------------|
| Source, path, position, prevent-user | `Wallpaper` → `WinCELinux.XPWallPaper` | **PR2** — core apply |
| Browse / upload / repository source | Same struct + FTP fields | **PR3** — repository apply (hydration) |
| Repository connections / files | `GetddlConnection`, `GetsRepositoryFiles` | Deferred — inline `repository` on apply (see PR4 ops doc) |
| SysView / template queue | `ExecuteLaterTemplate` in DAC | **PR4** — templates (optional) |
| Payload strategy spike + ADR | N/A | **PR0** — **Complete** |

---

## 3. PR breakdown

| PR | Scope | Status |
|----|-------|--------|
| **PR0** | FusionX trace, payload size spike tests, [ADR-0006](./adr/0006-windows-wallpaper-payload-strategy.md) | **Complete** |
| **PR1** | `SettingsKind.WindowsWallpaper`, `device_windows_wallpaper_settings`, contracts, payload builder, read-only GET API | **Complete** |
| **PR2** | Core apply (browse path, inline JSON ≤512), ack handler, execute-now / queue endpoints | **Complete** |
| **PR3** | Repository/upload apply, `repository_json` JSONB, task payload hydrator | **Complete** |
| **PR4** | Group bulk / template queue parity (optional) | **Complete** — see [windows-wallpaper-template-operations.md](./windows-wallpaper-template-operations.md) |

---

## 4. FusionX parity table

| FusionX UI | Agent struct (XP) | `SettingsKind` | PR |
|------------|-------------------|----------------|-----|
| Wallpaper Settings | `WinCELinux.XPWallPaper` | `WindowsWallpaper` | PR2 (core), PR3 (upload) |
| Signal | `{mac}&WPS` | `extra_data` | PR2 |
| Module type | `Wallpaper` | `device_tasks.module_name` | PR2 |

**Payload strategy:** [ADR-0006](./adr/0006-windows-wallpaper-payload-strategy.md)

---

## 5. Database

| Table | Purpose |
|-------|---------|
| `intellinode.device_windows_wallpaper_settings` | Per-device desired state: scalars + optional `repository_json` JSONB (PR3), version/apply columns |
| `intellinode.device_windows_wallpaper_settings_snapshots` | Immutable queue-time snapshots for repository/upload hydration (PR3) |

PostgreSQL enum `intellinode.settings_kind` includes `WindowsWallpaper` (PR1).

---

## 6. PR0 / PR1 artifacts

| Artifact | Path |
|----------|------|
| ADR | [docs/adr/0006-windows-wallpaper-payload-strategy.md](./adr/0006-windows-wallpaper-payload-strategy.md) |
| Payload builder | `src/Intellinode.Infrastructure/Services/WindowsWallpaperPayloadBuilder.cs` |
| Module constants | `src/Intellinode.Application/Contracts/Admin/WindowsWallpaperContracts.cs` |
| Spike tests | `tests/Intellinode.Infrastructure.Tests/Wallpaper/WindowsWallpaperPayloadSizeSpikeTests.cs` |
| Admin GET API | `GET /api/v1/admin/device-config/wallpaper/{macAddress}` |

## 7. PR2 / PR3 apply pipeline

| Artifact | Path |
|----------|------|
| Settings service | `src/Intellinode.Infrastructure/Services/WindowsWallpaperSettingsService.cs` |
| Ack handler | `src/Intellinode.Infrastructure/Services/WindowsWallpaperTaskAckHandler.cs` |
| Snapshot entity | `src/Intellinode.Domain/Entities/DeviceWindowsWallpaperSettingsSnapshot.cs` |
| Task hydrator (PR3) | `src/Intellinode.Infrastructure/Services/WindowsWallpaperTaskPayloadHydrator.cs` |
| Agent poll wrapper (PR3) | `src/Intellinode.Infrastructure/Services/WallpaperHydratingAgentTaskService.cs` |
| Execute-now / queue | `POST .../wallpaper/execute-now`, `POST .../wallpaper/queue` |
| Template / bulk / group (PR4) | `POST .../wallpaper/template-queue`, `POST .../wallpaper/execute-now/bulk`, `POST .../wallpaper/execute-now/group/{groupId}` |

Run spike tests:

```bash
dotnet test tests/Intellinode.Infrastructure.Tests/Intellinode.Infrastructure.Tests.csproj --filter "FullyQualifiedName~WindowsWallpaperPayloadSizeSpikeTests"
```

---

## 8. Related user settings (FusionX)

FusionX **User Settings** includes Screen Saver, Taskbar, User Interface, and Wallpaper. Screen Saver, Taskbar, User Interface, and Wallpaper (PR1–PR4) are implemented in Intellinode.

**Windows Wallpaper v1 apply surface is complete (PR0–PR4).**
