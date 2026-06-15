# Windows Wallpaper — Template / Bulk / Group (PR4)

FusionX **ExecuteLaterTemplate** parity (`WindowsWallpaperDAC`) plus bulk/group instant apply (same pattern as Screen Saver and Power Management).

---

## API endpoints

| Method | Route | Purpose |
|--------|-------|---------|
| POST | `/api/v1/admin/device-config/wallpaper/template-queue` | SysView template queue (`scheduleType`: `QueueTemplate`) |
| POST | `/api/v1/admin/device-config/wallpaper/execute-now/bulk` | Instant apply to multiple MAC targets |
| POST | `/api/v1/admin/device-config/wallpaper/execute-now/group/{groupId}` | Instant apply to active devices in group |

---

## Template queue

| Field | Value |
|-------|-------|
| `execution.scheduleType` | **`QueueTemplate`** |
| `execution.templateId` | SysView template id (> 0) |
| `execution.templateName` | SysView template name (required) |
| `function_name` (task) | **`QueueTemplate`** |
| Apply log `apply_mode` | **`template`** |

Settings body matches `/queue` (browse or repository path per PR2/PR3). Template metadata is stored on the API response and apply log message; agent `extra_data` remains `{mac}&WPS`.

---

## Bulk / group

- **InstantApply only** (no bulk queue in v1).
- Per-target results: `Pending` or `Blocked` with `reason`.
- Group resolves active enrolled devices in the group (`EnrollmentState.Active`).

---

## Repository catalog (deferred)

FusionX `GetddlConnection` / `GetsRepositoryFiles` dropdown helpers are **not** ported in v1 — admins supply `repository` metadata inline on apply requests (same as Screen Saver PR4). A shared repository catalog API can be added later when connection master data is available in Intellinode.

---

## Related

- [wallpaper-overview.md](./wallpaper-overview.md)
- [ADR-0006](./adr/0006-windows-wallpaper-payload-strategy.md)
