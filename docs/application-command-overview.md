# Windows Application Command — Overview

## 1. Overview

Intellinode is porting FusionX **Administration → Application command** to a modern ASP.NET API. v1 targets **Windows `:XP` devices only**.

PR0 ([ADR-0007](./adr/0007-windows-application-command-payload-strategy.md)) completed payload spike and strategy decision: **Option A inline JSON for all v1 scenarios** (no hydration).

---

## 2. Module map (FusionX UI → Intellinode PRs)

| FusionX UI area | FusionX module / struct | Intellinode PR |
|-----------------|-------------------------|----------------|
| Remote Application (path, params, alert user) | `Application` → `WinCELinux.Application` | **PR2** — core apply |
| Remote Command (command, timeout, output) | `Command` → `WinCELinux.Command` | **PR2** — core apply |
| Reference dropdowns (msg type, display time, timeout) | MUI XML `Windows_ucAplicationAndCommand` | **PR3** |
| SysView / template queue | `QueueTemplate` in handler | **PR4** — optional |
| Payload strategy spike + ADR | N/A | **PR0** — **Complete** |

---

## 3. PR breakdown

| PR | Scope | Status |
|----|-------|--------|
| **PR0** | FusionX trace, payload size spike tests, [ADR-0007](./adr/0007-windows-application-command-payload-strategy.md) | **Complete** |
| **PR1** | `SettingsKind.WindowsApplication` + `WindowsCommand`, `device_windows_application_command_settings`, contracts, payload builder, read-only GET API | **Complete** |
| **PR2** | Core apply (instant + queue), ack handlers, execute-now / queue endpoints | **Complete** |
| **PR3** | Reference dropdowns, validation hardening, optional command denylist | **Complete** |
| **PR4** | Group bulk / template queue parity (optional) | **Complete** |

---

## 4. FusionX parity table

| FusionX UI | Agent struct (XP) | `SettingsKind` | PR |
|------------|-------------------|----------------|-----|
| Remote Application | `WinCELinux.Application` | `WindowsApplication` | PR2 |
| Remote Command | `WinCELinux.Command` | `WindowsCommand` | PR2 |
| Signal (bulk) | `{mac}&196&Insert&` | `extra_data` `{mac}&196` | PR2 |
| Module type | `Application` / `Command` | `device_tasks.module_name` | PR2 |

**Payload strategy:** [ADR-0007](./adr/0007-windows-application-command-payload-strategy.md)

---

## 5. Database (planned PR1)

| Table | Purpose |
|-------|---------|
| `intellinode.device_windows_application_command_settings` | Per-device desired state: mode + application/command fields, version/apply columns |

PostgreSQL enum `intellinode.settings_kind` will gain `WindowsApplication` and `WindowsCommand` (PR1).

---

## 6. PR0 artifacts

| Artifact | Path |
|----------|------|
| ADR | [docs/adr/0007-windows-application-command-payload-strategy.md](./adr/0007-windows-application-command-payload-strategy.md) |
| Module constants | `src/Intellinode.Application/Contracts/Admin/WindowsApplicationCommandContracts.cs` |
| Payload builder | `src/Intellinode.Infrastructure/Services/WindowsApplicationCommandPayloadBuilder.cs` |
| Request validation / serialize | `src/Intellinode.Application/Validation/WindowsApplicationCommandRequestValidation.cs` |
| Spike tests | `tests/Intellinode.Infrastructure.Tests/ApplicationCommand/WindowsApplicationCommandPayloadSizeSpikeTests.cs` |

## 7. PR1 artifacts

| Artifact | Path |
|----------|------|
| Entity | `src/Intellinode.Domain/Entities/DeviceWindowsApplicationCommandSettings.cs` |
| Migration | `src/Intellinode.Infrastructure/Persistence/Migrations/20260616093936_AddDeviceWindowsApplicationCommandSettings.cs` |
| Settings service (read) | `src/Intellinode.Infrastructure/Services/WindowsApplicationCommandSettingsService.cs` |
| Admin GET API | `GET /api/v1/admin/device-config/application-command/{macAddress}?mode=Application\|Command` |
| Options | `WindowsApplicationCommand` in `appsettings.json` |
| Model tests | `tests/Intellinode.Infrastructure.Tests/ApplicationCommand/DeviceWindowsApplicationCommandSettingsModelTests.cs` |

## 8. PR2 artifacts

| Artifact | Path |
|----------|------|
| Apply service | `src/Intellinode.Infrastructure/Services/WindowsApplicationCommandSettingsService.cs` |
| Ack handler | `src/Intellinode.Infrastructure/Services/WindowsApplicationCommandTaskAckHandler.cs` |
| Validators | `src/Intellinode.Application/Validators/WindowsApplicationCommandValidators.cs` |
| Admin apply API | `POST .../execute-now`, `POST .../queue`, `GET .../apply-history/{macAddress}` |
| Service / ack / validation tests | `tests/Intellinode.Infrastructure.Tests/ApplicationCommand/` |

Run Application Command tests:

```bash
dotnet test tests/Intellinode.Infrastructure.Tests/Intellinode.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ApplicationCommand"
```

## 9. PR3 artifacts

| Artifact | Path |
|----------|------|
| Reference catalog | `src/Intellinode.Application/Validation/WindowsApplicationCommandReferenceCatalog.cs` |
| Validation policy (denylist) | `src/Intellinode.Application/Validation/WindowsApplicationCommandValidationPolicy.cs` |
| Reference API | `GET .../application-command/reference/options`, `.../message-types`, `.../display-times`, `.../timeouts` |
| Hardened validation | `WindowsApplicationCommandRequestValidation` + FluentValidation (reference values + denylist) |
| Config | `CommandDenylistEnabled`, `DeniedCommandPatterns` in `WindowsApplicationCommand` appsettings |
| Tests | `WindowsApplicationCommandReferenceCatalogTests`, extended validation/service tests |

Reference values mirror FusionX `Windows_ucAplicationAndCommand.ascx`:

| Dropdown | Agent values |
|----------|--------------|
| Message type | `1` Message box, `0` Information message box |
| Display time | `60`–`600` seconds (1–10 minutes) |
| Timeout | `0` Never, `5`, `30`, `60`, `120`, `180`, `300` seconds |

## 10. PR4 artifacts

| Artifact | Path |
|----------|------|
| Template queue | `POST .../application-command/template-queue` (`QueueTemplate`, SysView template id/name) |
| Bulk execute-now | `POST .../application-command/execute-now/bulk` |
| Group execute-now | `POST .../application-command/execute-now/group/{groupId}` |
| Bulk contracts | `WindowsApplicationCommandExecuteNowBulkRequest`, `WindowsApplicationCommandBulkResponse`, etc. |
| Validators | `WindowsApplicationCommandTemplateQueueRequestValidator`, bulk/group validators |
| Tests | `tests/Intellinode.Infrastructure.Tests/ApplicationCommand/WindowsApplicationCommandBulkTests.cs` |

---

## 11. Related FusionX administration settings

FusionX **Administration** also includes Environment Variable, Write Filter, and other modules. Application Command is the first administration module targeted in Intellinode; Environment Variable would be a separate track.

**Windows Application Command PR4 bulk/group/template queue is complete.**
