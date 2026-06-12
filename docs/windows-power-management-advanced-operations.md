# Windows Power Management — Advanced Operations (PR3)

FusionX parity for **AdvancePowerOption.aspx** / `XP_PowerPlan_AdvanceDetails`. Uses the same module, compact task reference, hydrator, and ack pipeline as [basic apply](./windows-power-management-operations.md).

---

## 1. FusionX trace summary

| Source | Finding |
|--------|---------|
| `AdvancePowerOption.aspx` | Tree of advanced option groups (Sleep extended, password on wakeup, USB selective suspend, processor states, etc.) with plan-specific dropdown catalogs |
| `XP_PowerPlan_AdvanceDetails.CurrentPlan_Xml` | XML blob stored per plan in FusionX; agent does **not** consume raw XML |
| `WindowsPowerManagementDAC.GetPowerManagement_Settings` | Builds flat `objPowerOptions[]` / `objPowerSettings[]` for `WinCELinux.XPPowerManagement` — basic + advanced groups together |
| `WindowsPowerManagementDAC.getPowerSettings` | When a value contains `"Minutes"`, strips suffix and sends numeric prefix only (`"10 Minutes"` → `"10"`, `"300 Minutes"` → `"300"`); labels like `Never`, `Enable`, `Off` pass through unchanged |
| `WindowsPowerMngmntHandler.ashx.cs` | Advanced-only UI changes still queue the **full** power struct (existing basic groups from DB + changed advanced groups) — Intellinode advanced apply uses read-merge-write on `settings_json` |
| Signal | `{macAddress}&PMO,{planName}` unchanged |

**ADR open question #5 (confirmed):** Changing only advanced options in FusionX still sends the complete `XPPowerManagement` payload. Intellinode merges incoming advanced `optionGroups[]` into the stored JSON by `strPowerOptionName` and preserves basic groups unless the request replaces that group.

**Extended fields:** Spike scenario 5 and FusionX max payloads include empty `strText3` / `strText4` when advanced groups are present. Intellinode adds these on merge when any advanced group exists (or when already present in stored JSON).

---

## 2. Reference API

Base: `/api/v1/admin/device-config/power-management/reference`

| Method | Path | Query | Description |
|--------|------|-------|-------------|
| GET | `/advanced-options` | `planName`, `optionName`, `includeInactive` | Advanced dropdown catalog grouped by `optionName` → `settings[]` → `values[]` |

Master table: `intellinode.windows_power_advanced_option_master` (seed ids 1001–1037, PR3 migration).

---

## 3. Apply APIs

Base: `/api/v1/admin/device-config/windows-power-management/advanced`

| Method | Path | Description |
|--------|------|-------------|
| POST | `/execute-now` | Instant advanced apply (`FunctionName`: `Now`) |
| POST | `/queue` | Queued advanced apply (`FunctionName`: `Update`) |
| POST | `/execute-now/bulk` | Bulk instant (partial success) |
| POST | `/execute-now/group/{groupId}` | Group instant (partial success) |

**Constraints:** Windows `:XP` only; at least one **advanced** option group required; `ModuleName` remains `"Power Management Settings"`.

**Merge semantics:**

1. Load existing `device_windows_power_management_settings.settings_json` (if any).
2. Upsert requested groups by `optionName`; within a group, upsert settings by `settingName`.
3. Normalize minute values per FusionX DAC rules.
4. Bump `settings_version`, set `pending_apply`, insert snapshot, queue compact ref `{"settingsVersion":N,"planName":"..."}`.

GET current (`/{macAddress}`) returns all groups in `optionGroups` and advanced subset in `advancedOptionGroups`.

---

## 4. Value normalization

Applied on basic and advanced paths via `NormalizeSettingValue`:

- `"10 Minutes"`, `"10 minutes"` → `"10"`
- `"300 Minutes"` → `"300"`
- `"Never"`, `"Enable"`, `"Sleep"`, processor percentages, etc. → unchanged (trimmed)

Admin UI should prefer catalog `valueText` from GET `/advanced-options`; display labels may include `" Minutes"` while agent values are numeric.

---

## 5. Error codes

Same as basic apply ([windows-power-management-operations.md](./windows-power-management-operations.md)) plus validation when no advanced option groups are supplied.

| Code | HTTP | Meaning |
|------|------|---------|
| `ValidationFailed` | 400 | Missing advanced options, non-`:XP` target, invalid plan |
| `ApplyBlocked` | 409 | Pending task on `"Power Management Settings"` |
| `InvalidPowerPlan` | 409 (blocked reason) | Unknown plan name |

---

## 6. Agent poll / ack

No PR3 changes to `AgentTaskService` hydrator or ack wiring. Hydrator remains snapshot-first; full hydrated payload may exceed 512 chars (expected for scenario 5 ≈2,617 chars); stored task ref stays ≤512.

---

## 7. Related docs

- [power-management-overview.md](./power-management-overview.md)
- [ADR-0004](./adr/0004-windows-power-management-payload-strategy.md)
- [windows-power-management-operations.md](./windows-power-management-operations.md) — basic apply
- [windows-power-management-reference-operations.md](./windows-power-management-reference-operations.md) — PR1 reference
- [windows-power-management-template-operations.md](./windows-power-management-template-operations.md) — PR4 SysView template queue
