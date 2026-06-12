# Windows Power Management — Operations Guide

## 1. Overview

Intellinode exposes REST APIs for **Windows Power Management** (power plan configuration) on **Windows `:XP` devices only** (v1). Admins configure the FusionX **System Settings → Power Management** basic UI: display timeout, sleep, hard disk, button actions, and system standby.

| Concept | Value |
|---------|-------|
| FusionX UI area | **Power Option** (basic dropdowns) |
| FusionX `module_name` | `Power Management Settings` (exact string) |
| Desired storage | `device_windows_power_management_settings` (1 row/device) |
| Settings kind (apply logs) | `WindowsPowerManagement` |
| Reference helpers | `GET .../power-management/reference/power-plans`, `.../timeouts`, `.../advanced-options` (PR3) |

**Basic vs advanced apply:** Basic endpoints live under `/windows-power-management` (PR2). Advanced tree apply (merge into stored JSON) lives under `/windows-power-management/advanced` — see [windows-power-management-advanced-operations.md](./windows-power-management-advanced-operations.md).

This module configures **power plans and options**. It is **not** the generic **Power Management** agent task used for shutdown/reboot — see [power-management-overview.md](./power-management-overview.md#4-separation-power-plan-settings-vs-agent-power-actions).

---

## 2. FusionX parity

| Item | Value |
|------|-------|
| Agent struct | `WinCELinux.XPPowerManagement` |
| Payload wrapper | `{ "WinCELinux": { "XPPowerManagement": { ... } } }` |
| Instant function | `"Now"` |
| Queued function | `"Update"` |
| Signal suffix (default) | `PMO` → `extra_data` = `{mac}&PMO,{planName}` |
| Payload delivery | **Compact task reference** in `device_tasks.function_parameter`; full JSON hydrated at poll (ADR-0004 Option B) |
| `TaskID` in payload | Legacy numeric task id (`device_tasks.legacy_task_id`) |
| `AgentAction` | From request `execution.agentAction` (default `0`) |

**Basic UI option groups (PR2):**

| FusionX group | Request field(s) |
|---------------|------------------|
| Display | `displayTimeoutText` |
| Hard disk | `hardDiskTimeoutText` |
| Sleep | `sleepTimeoutText` |
| Power buttons and lid | `powerButtonAction`, `sleepButtonAction` |
| System standby | `systemStandbyTimeoutText` |

Alternatively pass explicit `optionGroups[]` with FusionX `OptionName` / `SettingName` / `SettingValue` pairs.

**Stored task reference (≤512 chars):**

```json
{"settingsVersion":3,"planName":"Balanced"}
```

**Hydrated agent payload (poll time):**

```json
{
  "WinCELinux": {
    "XPPowerManagement": {
      "strPowerSchemaName": "Balanced",
      "blIsActive": true,
      "objPowerOptions": [ ... ],
      "Operation": "Update",
      "Index": "1",
      "TaskID": 42,
      "AgentAction": 0
    }
  }
}
```

---

## 3. API reference (admin)

Base path: `/api/v1/admin/device-config/windows-power-management`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/{macAddress}` | Current desired settings + version/apply status |
| GET | `/apply-history/{macAddress}` | Apply history (`status`, `page`, `pageSize`, `fromUtc`, `toUtc`) |
| POST | `/execute-now` | Instant apply (`scheduleType`: `InstantApply`) |
| POST | `/queue` | Scheduled apply (`scheduleType`: `Queue`) |
| POST | `/template-queue` | SysView template queue (`scheduleType`: `QueueTemplate`, PR4) — see [template ops](./windows-power-management-template-operations.md) |
| POST | `/execute-now/bulk` | Instant apply same settings to many MACs |
| POST | `/execute-now/group/{groupId}` | Instant apply to active group members |

**Reference helpers:** see [windows-power-management-reference-operations.md](./windows-power-management-reference-operations.md).

**Common error codes**

| Code | HTTP | Meaning |
|------|------|---------|
| `FeatureDisabled` | 404 | `WindowsPowerManagement:Enabled=false` or `ReadOnly=true` on writes |
| `DeviceNotFound` | 404 | MAC not enrolled in tenant |
| `ApplyBlocked` | 409 | Pending/InProcess task for `Power Management Settings`, or enrollment not managed |
| `ValidationFailed` | 400 | FluentValidation or invalid target (non-`:XP` MAC) |
| `GroupNotFound` | 404 | Group id invalid (group endpoints only) |
| `LegacyBehaviorExecutionFailed` | 502 | Unexpected service error |

**Apply blocking:** A pending **Power Management Settings** task blocks another apply for the same module. It does **not** block generic shutdown/reboot tasks (`moduleName`: `Power Management`).

Bulk and group endpoints return **HTTP 200** when orchestration succeeds, even if some targets are blocked.

---

## 4. Configuration (`appsettings.json` → `WindowsPowerManagement`)

| Setting | Default | Purpose |
|---------|---------|---------|
| `Enabled` | `true` | Master switch |
| `ReadOnly` | `false` | Blocks writes (execute-now, queue, template-queue, bulk, group) |
| `LegacySummaryEnabled` | `true` | FusionX HTML summary in responses |
| `DefaultSignalSuffix` | `PMO` | `extra_data` = `{mac}&PMO,{planName}` |

---

## 5. Agent pipeline

1. **Admin apply** upserts `device_windows_power_management_settings`, bumps `settings_version`, writes apply log (`Pending`).
2. **Queue** also inserts `device_windows_power_management_settings_snapshots` for the version (immutable JSON for hydration race safety).
3. **Task row** stores compact `function_parameter` + `extra_data` `{mac}&PMO,{planName}`.
4. **Agent poll** (`GET /api/v1/agent/tasks/pending`): `AgentTaskService` hydrates Power Management tasks via `IWindowsPowerManagementTaskPayloadHydrator` (snapshot-first, then live row).
5. **Agent ack** (`POST /api/v1/agent/tasks/ack`): `WindowsPowerManagementTaskAckHandler` sets `lastAppliedVersion`, `lastApplyStatus`, clears `pendingApply`, writes final apply log.

---

## 6. Database (PR2 additions)

| Table | Purpose |
|-------|---------|
| `device_windows_power_management_settings` | Live desired state (PR1 schema; PR2 apply) |
| `device_windows_power_management_settings_snapshots` | Immutable JSON per `settings_version` at queue time |

---

## 7. Related docs

- [power-management-overview.md](./power-management-overview.md) — module map and PR breakdown
- [ADR-0004](./adr/0004-windows-power-management-payload-strategy.md) — compact reference + hydration strategy
- [windows-power-management-reference-operations.md](./windows-power-management-reference-operations.md) — power plan / timeout reference APIs
