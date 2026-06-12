# Windows Power Management — Overview

## 1. Overview

Intellinode is porting FusionX **System Settings → Power Management** (power plan configuration) to a modern ASP.NET API. v1 targets **Windows `:XP` devices only**; Linux power settings are deferred.

This module configures Windows power **plans and options** (display timeout, sleep, hard disk, button actions, advanced tree settings). It is **not** the generic **Power Management** agent task used for shutdown/reboot actions — see [§4 Separation](#4-separation-power-plan-settings-vs-agent-power-actions).

PR0 ([ADR-0004](./adr/0004-windows-power-management-payload-strategy.md)) completed payload spike and Option B hydration decision. **PR1** adds reference master data, device settings schema, payload builder/hydrator stubs, and read-only reference APIs. **PR2** adds basic apply (5 FusionX option groups), compact task reference + hydration in `AgentTaskService`, and ack handling. **PR3** adds advanced option tree apply (same hydration pattern). PR4 adds optional templates.

---

## 2. Module map (FusionX UI → Intellinode PRs)

| FusionX UI area | FusionX module / struct | Intellinode PR |
|-----------------|-------------------------|----------------|
| Power Option (basic dropdowns) | `Power Management Settings` → `WinCELinux.XPPowerManagement` | **PR2** — basic apply |
| Advanced power option tree | Same module + `XP_PowerPlan_AdvanceDetails` XML | **PR3** — advanced apply |
| Power plan list / option catalog | `XP_PowerPlan`, advance XML presets | **PR1** — reference |
| SysView / template queue | `ExecuteLaterTemplate` path in DAC | **PR4** — templates (optional) |
| Payload strategy spike + ADR | N/A | **PR0** — **Complete** |

---

## 3. PR breakdown

| PR | Scope | Status |
|----|-------|--------|
| **PR0** | FusionX trace, payload size spike tests, [ADR-0004](./adr/0004-windows-power-management-payload-strategy.md) (Option B decision) | **Complete** |
| **PR1** | `SettingsKind.WindowsPowerManagement`, reference master tables + seed, `device_windows_power_management_settings`, contracts, payload builder + hydrator stub, read-only reference APIs | **Complete** — see [windows-power-management-reference-operations.md](./windows-power-management-reference-operations.md) |
| **PR2** | Basic apply (5 FusionX option groups), compact task reference + hydration wiring, ack | **Complete** — see [windows-power-management-operations.md](./windows-power-management-operations.md) |
| **PR3** | Advanced power option tree apply (`XP_PowerPlan_AdvanceDetails` parity), same hydration pattern | **Complete** — see [windows-power-management-advanced-operations.md](./windows-power-management-advanced-operations.md) |
| **PR4** | Template / SysView queue parity (optional) | **Complete** — see [windows-power-management-template-operations.md](./windows-power-management-template-operations.md) |

---

## 4. Separation: power plan settings vs agent power actions

| Concern | Module name | Purpose | Intellinode scope |
|---------|-------------|---------|-------------------|
| **Power plan configuration** | `Power Management Settings` | Apply Balanced/High performance plan options via `XPPowerManagement` | **This module** (PR1–PR4) |
| **Agent actions (Shutdown, Reboot, etc.)** | `Power Management` | Generic queued tasks (`functionName` = `Shutdown`, …) with empty or action-specific parameters | **Existing** admin task queue (`Intellinode.Api.http` samples); **do not merge** with power plan settings |

FusionX schedule fields for plan apply (`WindowsPowerMngmntHandler.SetObjectValues` / `WindowsPowerManagementDAC`):

- `objSchedule.ModuleType` = `"Power Management Settings"`
- `objSchedule.ModuleName` = power plan name (e.g. `Balanced`, `High performance`) — **not** `XP_PowerPlan` table name; plan comes from `SplitPlan(selectedddlPowerplan)` / `XP_PowerPlan.Power_Plan`
- Signal: `{macAddress}&PMO,{planName}` (FusionX handler)

---

## 5. FusionX parity table

| FusionX `module_name` / UI | Agent struct (XP) | `SettingsKind` | PR |
|----------------------------|-------------------|----------------|-----|
| Power Management Settings | `WinCELinux.XPPowerManagement` | `WindowsPowerManagement` | PR2 (basic), PR3 (advanced) |
| Power plan catalog | `XP_PowerPlan` / advance XML | Reference only | **PR1** |
| *(out of scope)* Power Management shutdown tasks | Generic action tasks | N/A | Pre-existing task API |

**Payload strategy:** [ADR-0004](./adr/0004-windows-power-management-payload-strategy.md) — **Option B** (JSONB + compact task reference + server-side hydration at poll time).

---

## 6. Database

| Table | Purpose |
|-------|---------|
| `intellinode.windows_power_plan_master` | FusionX power plan names (`Balanced`, `High performance`, `Power saver`) |
| `intellinode.windows_power_timeout_master` | Basic UI dropdown catalog by category |
| `intellinode.windows_power_advanced_option_master` | Advanced tree dropdown catalog (PR3) |
| `intellinode.device_windows_power_management_settings` | Per-device desired plan state: `active_plan_name`, `settings_json` JSONB, version/apply columns |
| `intellinode.device_windows_power_management_settings_snapshots` | Immutable JSON per `settings_version` at queue time (hydration race safety, PR2) |

PostgreSQL enum `intellinode.settings_kind` includes `WindowsPowerManagement` (PR1).

---

## 7. Reference API (admin, PR1)

Base path: `/api/v1/admin/device-config/power-management/reference`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/power-plans` | Active power plans; `includeInactive` optional |
| GET | `/timeouts` | Timeout/action catalog; `category` + `includeInactive` optional |
| GET | `/advanced-options` | Advanced option catalog; `planName`, `optionName`, `includeInactive` optional (PR3) |

**Configuration:** `PowerManagementReference:Enabled` (default `true`). Ops doc: [windows-power-management-reference-operations.md](./windows-power-management-reference-operations.md).

**Common error codes:** `FeatureDisabled` (404), `LegacyBehaviorExecutionFailed` (502).

---

## 8. Apply APIs (admin, PR2)

Base path: `/api/v1/admin/device-config/windows-power-management`

Ops doc: [windows-power-management-operations.md](./windows-power-management-operations.md)

| Method | Path | Description |
|--------|------|-------------|
| GET | `/{macAddress}` | Current desired settings + apply status |
| GET | `/apply-history/{macAddress}` | Task + apply log history |
| POST | `/execute-now` | Instant apply (`FunctionName`: `Now`) |
| POST | `/queue` | Queued apply (`FunctionName`: `Update`) |
| POST | `/execute-now/bulk` | Bulk instant apply |
| POST | `/execute-now/group/{groupId}` | Group instant apply |
| POST | `/template-queue` | SysView template queue (`scheduleType`: `QueueTemplate`, PR4) |

Task contract (per ADR-0004):

| Field | Value |
|-------|-------|
| `ModuleName` | `Power Management Settings` |
| `FunctionName` | `Now` / `Update` / `QueueTemplate` (PR4 SysView) |
| `ExtraData` | `{macAddress}&PMO,{planName}` |
| `FunctionParameter` (stored) | `{"settingsVersion":<N>}` or with `"planName"` |
| Agent poll `functionParameter` | Hydrated full `{"WinCELinux":{"XPPowerManagement":{…}}}` via `AgentTaskService` |

**Planned (PR3+):** ~~advanced power option tree apply~~ **Complete** — see [§9 Advanced apply APIs](#9-advanced-apply-apis-admin-pr3) and [windows-power-management-advanced-operations.md](./windows-power-management-advanced-operations.md).

---

## 9. Advanced apply APIs (admin, PR3)

Base path: `/api/v1/admin/device-config/windows-power-management/advanced`

Ops doc: [windows-power-management-advanced-operations.md](./windows-power-management-advanced-operations.md)

| Method | Path | Description |
|--------|------|-------------|
| POST | `/execute-now` | Instant advanced apply (merge into stored JSON) |
| POST | `/queue` | Queued advanced apply |
| POST | `/execute-now/bulk` | Bulk instant apply |
| POST | `/execute-now/group/{groupId}` | Group instant apply |
| POST | `/template-queue` | Advanced SysView template queue (merge, PR4) |

Same task contract as §8. Advanced apply merges `optionGroups[]` by name; basic groups from prior applies are preserved.

---

## 10. Template / SysView queue (admin, PR4)

Ops doc: [windows-power-management-template-operations.md](./windows-power-management-template-operations.md)

| Method | Path | Description |
|--------|------|-------------|
| POST | `/template-queue` | Basic settings via SysView template reference |
| POST | `/advanced/template-queue` | Advanced settings merge via SysView template reference |

`function_name` = `QueueTemplate`; agent signal and hydration unchanged.

---

## 11. Notes

- **v1 Windows `:XP` only** — MAC suffix and agent payloads follow existing Intellinode Windows module conventions.
- **Linux** `Linux_ucPowerSettings` is out of scope for v1.
- **PR1 complete** — reference GET APIs, payload builder/hydrator in DI.
- **PR2 complete** — basic apply APIs, snapshot table, hydration in `AgentTaskService`, ack handler.
- **PR3 complete** — advanced apply APIs, advanced reference catalog, merge into `settings_json`. See [windows-power-management-advanced-operations.md](./windows-power-management-advanced-operations.md).
- **PR4 complete** — SysView / template queue (`QueueTemplate`). See [windows-power-management-template-operations.md](./windows-power-management-template-operations.md).
- **Windows Power Management v1 apply surface is complete (PR0–PR4).**
