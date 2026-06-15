# Windows User Interface / Autologon — Bulk, Group, Legacy Summary (PR5)

FusionX **User Settings → User Interface** parity for multi-target instant apply, group apply, and legacy admin summary objects.

---

## API endpoints

| Method | Route | Purpose |
|--------|-------|---------|
| POST | `/api/v1/admin/device-config/user-interface/execute-now/bulk` | Instant autologon apply to multiple MAC targets |
| POST | `/api/v1/admin/device-config/user-interface/execute-now/group/{groupId}` | Instant apply to active enrolled devices in group |

Template queue remains on PR3 (`/template-queue`).

---

## Bulk / group behavior

- **InstantApply only** (no bulk scheduled queue in v1).
- **`:XP` targets only** — non-XP MAC suffixes return `Blocked` / `UnsupportedOsType`.
- Per-target qualification in `data.results[]`:
  - `Pending` — task queued
  - `Blocked` with `reason`:
    - `PendingTaskExists` — Autologon task already pending
    - `InProcessTaskExists` — Autologon task in process on agent
    - `EnrollmentStateBlocked` — device not in managed enrollment state
    - `DeviceNotFound`, `UnsupportedOsType`, `ValidationFailed`
- Single-target apply returns **409 ApplyBlocked** with FusionX-style message (e.g. `Autologon settings are pending`).

---

## Legacy summary

When `options.returnLegacySummary` is true and `WindowsUserInterface:LegacySummaryEnabled` is set in config:

| Response field | Value |
|----------------|-------|
| `legacySummary.errorMsg` | `...$ApplyGreenSuccess` |
| `legacySummary.qualifiedMsg` | Accepted target count (bulk) or `"1"` (single) |
| `legacySummary.dtApproved` | `[]` |
| `legacySummary.htmlData` | `""` |

---

## Dry run

`options.dryRun: true` on bulk/group returns qualification results without persisting tasks. Dry run evaluates enrollment, OS suffix, and pending/in-process Autologon tasks.

---

## Related

- Module: `Autologon` — payload `WinCELinux.XPAutologon`
- PR4 admin controller + `.http` samples
- PR3 template queue + agent hydration pipeline
