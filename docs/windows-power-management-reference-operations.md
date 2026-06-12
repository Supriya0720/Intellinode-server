# Windows Power Management — Reference Operations (PR1)

Read-only admin APIs for FusionX **System Settings → Power Management** dropdown catalogs. Apply endpoints are PR2+.

## Base path

`/api/v1/admin/device-config/power-management/reference`

**Authorization:** Admin bearer token required.

**Configuration:** `appsettings.json` → `PowerManagementReference:Enabled` (default `true`). When `false`, endpoints return **404** `FeatureDisabled`.

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/power-plans` | Active Windows power plans (`Balanced`, `High performance`, `Power saver`) |
| GET | `/timeouts` | Timeout/action catalog; optional `category` filter |

### Query parameters

| Parameter | Applies to | Default | Description |
|-----------|------------|---------|-------------|
| `includeInactive` | both | `false` | Include inactive master rows |
| `category` | `/timeouts` | *(all)* | `Display`, `Sleep`, `HardDisk`, `PowerButton`, `SleepButton`, `SystemStandby` |

## Common error codes

| Code | HTTP | Meaning |
|------|------|---------|
| `FeatureDisabled` | 404 | `PowerManagementReference:Enabled=false` |
| `LegacyBehaviorExecutionFailed` | 502 | Unexpected read failure |

## Database sources

| Table | Purpose |
|-------|---------|
| `intellinode.windows_power_plan_master` | FusionX `XP_PowerPlan` plan names |
| `intellinode.windows_power_timeout_master` | Basic UI dropdown values (minutes + button actions) |

Seeded from FusionX `UCWindowsPowerManagement.ascx` / `AdvancePowerOption.aspx` catalogs.

## Related

- Overview: [power-management-overview.md](./power-management-overview.md)
- Payload strategy: [ADR-0004](./adr/0004-windows-power-management-payload-strategy.md)
- Device settings table (PR1 schema, PR2 apply): `intellinode.device_windows_power_management_settings`

## Notes

- **Windows `:XP` only** for apply (PR2+); reference data is Windows-oriented.
- **Not** the generic `Power Management` shutdown task module — see overview §4.
- FusionX DAC may strip `" Minutes"` from persisted agent values; PR2 apply normalizes display strings.
