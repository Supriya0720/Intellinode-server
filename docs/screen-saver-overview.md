# Windows Screen Saver — Overview

## 1. Overview

Intellinode is porting FusionX **User Settings → User Interface Settings → Screen Saver Settings** to a modern ASP.NET API. v1 targets **Windows `:XP` devices only**; Linux `Lx_ScreenSaver` is deferred to a separate track.

This module configures the Windows screensaver (active `.scr` or `(None)`, wait timeout, password-on-resume, prevent-user-changes). It is **not** the **Screen Saver Logs** FDM report module — see [§4 Separation](#4-separation-screen-saver-settings-vs-logs).

PR0 ([ADR-0005](./adr/0005-windows-screen-saver-payload-strategy.md)) completed payload spike and split-strategy decision: **Option A inline JSON for PR2 core**, **Option B hydration for PR3 repository/upload**.

---

## 2. Module map (FusionX UI → Intellinode PRs)

| FusionX UI area | FusionX module / struct | Intellinode PR |
|-----------------|-------------------------|----------------|
| Screen saver name, wait, password | `ScreenSaver` → `WinCELinux.XPScreenSaver` | **PR2** — core apply |
| Browse / upload / repository source | Same struct + FTP fields | **PR3** — repository apply (hydration) |
| Screensaver list on device | `Input_prcGetScreenSaverList` | **PR1** — catalog stub |
| SysView / template queue | `ExecuteLaterTemplate` in DAC | **PR4** — templates (optional) |
| Payload strategy spike + ADR | N/A | **PR0** — **Complete** |
| Linux slideshow screen saver | `Lx_ScreenSaver` | **PR5** — deferred |

---

## 3. PR breakdown

| PR | Scope | Status |
|----|-------|--------|
| **PR0** | FusionX trace, payload size spike tests, [ADR-0005](./adr/0005-windows-screen-saver-payload-strategy.md) (split Option A/B decision) | **Complete** |
| **PR1** | `SettingsKind.WindowsScreenSaver`, `device_windows_screen_saver_settings`, contracts, payload builder, read-only GET API | **Complete** |
| **PR2** | Core apply (browse path, inline JSON ≤512), ack handler, execute-now / queue endpoints | **Complete** |
| **PR3** | Repository/upload apply, `repository_json` JSONB, task payload hydrator | **Complete** |
| **PR4** | Group bulk / template queue parity (optional) | **Complete** |
| **PR5** | Linux `Lx_ScreenSaver` (separate API) | Deferred |
| **PR6** | Screen saver logs report API (optional) | Deferred |

---

## 4. Separation: screen saver settings vs logs

| Concern | FusionX module | Purpose | Intellinode scope |
|---------|----------------|---------|-------------------|
| **Screen saver configuration** | `ScreenSaver` / `XPScreenSaver` | Apply saver name, timeout, password flag | **This module** (PR1–PR3) |
| **Screen saver event logs** | `ScreenSaver_Logs_Handler`, FDM reports | Historical log reporting | **Deferred** (PR6); `ScreensaverLogsEnabled` on agent advanced settings already exists |

FusionX schedule fields for screen saver apply (`SetScreenSaverSettingsfromGUI` / `WindowsScreenSaverDAC`):

- `objSchedule.ModuleType` = `"ScreenSaver"`
- `objSchedule.ModuleName` = `string.Empty`
- Signal: `{macAddress}&SCR`

---

## 5. FusionX parity table

| FusionX `module_name` / UI | Agent struct (XP) | `SettingsKind` | PR |
|----------------------------|-------------------|----------------|-----|
| Screen Saver Settings | `WinCELinux.XPScreenSaver` | `WindowsScreenSaver` | PR2 (core), PR3 (upload) |
| Screensaver list | `Input_prcGetScreenSaverList` | Catalog reference | **PR1** |
| Screen saver logs | `WindowsScreenSaverLogs` | N/A | PR6 (deferred) |
| Linux screen saver | `Lx_ScreenSaver` | `LinuxScreenSaver` (proposed) | PR5 |

**Payload strategy:** [ADR-0005](./adr/0005-windows-screen-saver-payload-strategy.md)

- **PR2:** Option A — full inline JSON in `device_tasks.function_parameter` (431–497 chars for core scenarios).
- **PR3:** Option B — JSONB + `{"settingsVersion":N}` compact reference + hydration when repository metadata present.

---

## 6. Database (planned)

| Table | Purpose |
|-------|---------|
| `intellinode.device_windows_screen_saver_settings` | Per-device desired state: scalars + optional `repository_json` JSONB (PR3), version/apply columns |
| `intellinode.device_windows_screen_saver_settings_snapshots` | Optional immutable JSON per `settings_version` at queue time (PR3 hydration race safety) |

PostgreSQL enum `intellinode.settings_kind` will include `WindowsScreenSaver` (PR1).

---

## 7. PR0 artifacts

| Artifact | Path |
|----------|------|
| ADR | [docs/adr/0005-windows-screen-saver-payload-strategy.md](./adr/0005-windows-screen-saver-payload-strategy.md) |
| Payload builder (spike) | `src/Intellinode.Infrastructure/Services/WindowsScreenSaverPayloadBuilder.cs` |
| Module constants | `src/Intellinode.Application/Contracts/Admin/WindowsScreenSaverContracts.cs` |
| Spike tests | `tests/Intellinode.Infrastructure.Tests/ScreenSaver/WindowsScreenSaverPayloadSizeSpikeTests.cs` |

Run spike tests:

```bash
dotnet test tests/Intellinode.Infrastructure.Tests/Intellinode.Infrastructure.Tests.csproj --filter "FullyQualifiedName~WindowsScreenSaverPayloadSizeSpikeTests"
```

---

## 8. Related user settings (FusionX, not yet in Intellinode)

FusionX **User Settings** also includes Taskbar, User Interface, and Wallpaper. Screen saver was the first module in this group planned for Intellinode porting; Wallpaper PR1 adds the read model for the final module (see [wallpaper-overview.md](./wallpaper-overview.md)).
