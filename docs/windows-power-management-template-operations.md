# Windows Power Management — Template / SysView Queue (PR4)

FusionX **ExecuteLaterTemplate** parity (`prc_TaskManager_ExecuteLater_Sysview` in `WindowsPowerManagementDAC`). Uses the same Option B hydration pipeline as [basic](./windows-power-management-operations.md) and [advanced](./windows-power-management-advanced-operations.md) apply.

---

## 1. FusionX trace summary

| Source | Finding |
|--------|---------|
| Schedule types | `InstantApply` → execute now; `QUEUE` → `ExecuteLater`; anything else → `ExecuteLaterTemplate` |
| Stored procedure | `prc_TaskManager_ExecuteLater_Sysview` receives serialized `XPPowerManagement` **plus** `TemplateName` and `TemplateID` |
| Agent payload | Same binary/JSON power struct as regular queue — template metadata is admin/audit side in FusionX |
| Group profile | `Insert_Update_Group_Profile_Info(..., isInstant=false)` for template queue |

Intellinode stores template id/name on the **API response** and **apply log message**; agent `extra_data` remains `{macAddress}&PMO,{planName}` so routing is unchanged.

---

## 2. API endpoints

### Basic template queue

`POST /api/v1/admin/device-config/windows-power-management/template-queue`

Same settings shape as `/queue` (basic 5 option groups or `optionGroups[]`).

### Advanced template queue

`POST /api/v1/admin/device-config/windows-power-management/advanced/template-queue`

Same merge semantics as `/advanced/queue` (upsert advanced groups into stored JSON).

---

## 3. Request contract

| Field | Value |
|-------|-------|
| `execution.scheduleType` | **`QueueTemplate`** (required) |
| `execution.templateId` | SysView template id (> 0) |
| `execution.templateName` | SysView template name (required) |
| `execution.agentAction` | Default `"0"` |

Validators mirror SystemSetting template queue (`templateId` + `templateName` required).

---

## 4. Task row mapping

| Field | Template queue value |
|-------|----------------------|
| `module_name` | `Power Management Settings` |
| `function_name` | **`QueueTemplate`** |
| `function_parameter` | Compact `{"settingsVersion":N,"planName":"..."}` (≤512) |
| `extra_data` | `{macAddress}&PMO,{planName}` (unchanged) |
| Apply log `apply_mode` | **`template`** |

Hydrator and ack handlers treat `QueueTemplate` like `Update` for payload delivery; apply history shows `applyMode=template`.

---

## 5. Response

Queue response includes optional template block (same shape as SystemSetting):

```json
{
  "success": true,
  "data": {
    "execution": { "scheduleType": "QueueTemplate", "status": "Pending", ... },
    "template": { "templateId": 101, "templateName": "BranchPowerTemplate" }
  }
}
```

---

## 6. Scope notes

- **No template library CRUD** in Intellinode v1 — `templateId` / `templateName` are opaque SysView references; settings payload is still supplied in the request body (FusionX replays template XML into the struct before queue).
- **No bulk/group template queue** in v1 (SystemSetting parity is single-target only).
- **Windows `:XP` only** — same as PR2–PR3.

---

## 7. Related docs

- [power-management-overview.md](./power-management-overview.md)
- [windows-power-management-operations.md](./windows-power-management-operations.md)
- [windows-power-management-advanced-operations.md](./windows-power-management-advanced-operations.md)
