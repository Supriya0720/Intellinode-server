# Windows Screen Saver — Template / Bulk / Group (PR4)

FusionX **ExecuteLaterTemplate** parity (`WindowsScreenSaverDAC`) plus bulk/group instant apply (same pattern as Power Management and Computer Name).

---

## API endpoints

| Method | Route | Purpose |
|--------|-------|---------|
| POST | `/api/v1/admin/device-config/screen-saver/template-queue` | SysView template queue (`scheduleType`: `QueueTemplate`) |
| POST | `/api/v1/admin/device-config/screen-saver/execute-now/bulk` | Instant apply to multiple MAC targets |
| POST | `/api/v1/admin/device-config/screen-saver/execute-now/group/{groupId}` | Instant apply to active devices in group |

---

## Template queue

| Field | Value |
|-------|-------|
| `execution.scheduleType` | **`QueueTemplate`** |
| `execution.templateId` | SysView template id (> 0) |
| `execution.templateName` | SysView template name (required) |
| `function_name` (task) | **`QueueTemplate`** |
| Apply log `apply_mode` | **`template`** |

Settings body matches `/queue` (browse or repository path per PR2/PR3). Template metadata is stored on the API response and apply log message; agent `extra_data` remains `{mac}&SCR`.

---

## Bulk / group

- **InstantApply only** (no bulk queue in v1).
- Per-target results: `Pending` or `Blocked` with `reason`.
- Group resolves active enrolled devices in the group (`EnrollmentState.Active`).

---

## Apply patch script

If partial-class hosts were not updated automatically, run:

```powershell
./scripts/apply-screen-saver-pr4-patches.ps1
```

---

## Related

- [screen-saver-overview.md](./screen-saver-overview.md)
- [ADR-0005](./adr/0005-windows-screen-saver-payload-strategy.md)
