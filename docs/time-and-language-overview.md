# Time and Language — Overview

## 1. Overview

Intellinode is porting FusionX **System Settings → Time and Language** to a modern ASP.NET API. v1 targets **Windows `:XP` devices only**; Linux is deferred.

PR1 (reference scope) provides **read-only master data** — locations, regions/languages, and Windows time zones. PR2–PR4 add the four Windows apply modules: date/time setup, region & location, and regional format. **Windows Time & Language v1 apply surface is complete (PR1–PR4).**

---

## 2. Module map (FusionX UI → Intellinode PRs)

| FusionX UI area | FusionX module(s) | Intellinode PR |
|-----------------|-------------------|----------------|
| Date and Time Setup → DateTime | DateTime | PR2 — `WindowsDateTimeSetup` |
| Date and Time Setup → TimeZone | TimeZone | PR2 — `WindowsDateTimeSetup` |
| Date and Time Setup → TimeServer | TimeServerSynchro | PR2 — `WindowsDateTimeSetup` |
| Region and Location | Region And Location Settings | PR3 — `WindowsRegionLocation` |
| Date & Time Format | Regional Settings | PR4 — `WindowsRegionalFormat` |
| Reference dropdowns (locations, regions, time zones, format presets) | Master data / static presets | **PR1** + PR4 `format-presets` |

---

## 3. PR breakdown

| PR | Scope | Status |
|----|-------|--------|
| **PR1** | Domain enums (`WindowsDateTimeApplyMode`, `SettingsKind` values), master tables, seed data, read-only GET reference APIs, docs | **Implemented** |
| **PR2** | `device_windows_date_time_settings`, date/time/timezone/time-server apply, tasks, payloads, ack | **Implemented** — see [windows-date-time-setup-operations.md](./windows-date-time-setup-operations.md) |
| **PR3** | Region and location per-device settings and apply | **Implemented** — see [windows-region-location-operations.md](./windows-region-location-operations.md) |
| **PR4** | Regional format per-device settings and apply | **Implemented** — see [windows-regional-format-operations.md](./windows-regional-format-operations.md) |

---

## 4. FusionX parity table

| FusionX `module_name` / UI | Agent struct (XP) | `SettingsKind` | PR |
|----------------------------|-------------------|----------------|-----|
| DateTime | `WinCELinux.XPDATE_TIME` (DtDate/DtTime) | `WindowsDateTimeSetup` | **PR2** — [ops doc](./windows-date-time-setup-operations.md) |
| TimeZone | `WinCELinux.XPDATE_TIME` (strTimeZone + MUI_Display) | `WindowsDateTimeSetup` | **PR2** |
| TimeServerSynchro | `WinCELinux.XPDATE_TIME` (TimeServer) | `WindowsDateTimeSetup` | **PR2** |
| Region And Location Settings | `WinCELinux.RegionAndLocation` | `WindowsRegionLocation` | **PR3** — [ops doc](./windows-region-location-operations.md) |
| Regional Settings | `WinCELinux.RegionalSettings` | `WindowsRegionalFormat` | **PR4** — [ops doc](./windows-regional-format-operations.md) |
| *(master data only)* | N/A | N/A | PR1 |

**Master data sources (FusionX):**

- **Locations / regions:** SQL table `RegionAndLocationMaster` — `Identifier` `L` = geographic location, `R` = region/language with optional `BCP47Code`.
- **Time zones:** Hardcoded `<option>` list in `UCWindowsDateTimeSettings.ascx` (`value` = display text, `class` = Windows TZ key / MUI_Display id).
- **Format presets:** Static list in Intellinode `GET .../reference/format-presets` (no DB table).
- **Excluded from location dropdowns:** id `39070` / value `World` (same filter in Intellinode `GET locations`).

---

## 5. Reference API (admin, PR1 + PR4)

Base path: `/api/v1/admin/device-config/time-and-language/reference`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/locations` | Active geographic locations (`Identifier = L`); excludes World / 39070 |
| GET | `/regions` | Active regions/languages (`Identifier = R`) with BCP47 codes |
| GET | `/time-zones` | Active Windows time zones (display name + `windowsTzKey`) |
| GET | `/format-presets` | Static common date/time format tokens (PR4 helper) |

**Query:** `includeInactive=false` (default) — when `true`, returns inactive master rows (locations/regions/time-zones only).

**Configuration:** `appsettings.json` → `TimeAndLanguageReference:Enabled` (default `true`). When `false`, all reference endpoints return **404** `FeatureDisabled`.

**Common error codes**

| Code | HTTP | Meaning |
|------|------|---------|
| `FeatureDisabled` | 404 | `TimeAndLanguageReference:Enabled=false` |
| `LegacyBehaviorExecutionFailed` | 502 | Unexpected read failure |

---

## 6. Database

| Table | Purpose |
|-------|---------|
| `intellinode.region_and_location_master` | FusionX `RegionAndLocationMaster` subset + room to expand |
| `intellinode.windows_time_zone_master` | FusionX `ddlTimeZone` options (display + Windows key) |
| `intellinode.device_windows_date_time_settings` | Per-device date/time/timezone/NTP desired state (PR2) |
| `intellinode.device_windows_region_location_settings` | Per-device geo + language locale desired state (PR3) |
| `intellinode.device_windows_regional_format_settings` | Per-device date/time display format desired state (PR4) |

PostgreSQL enum `intellinode.settings_kind` includes (for apply logs):

- `WindowsDateTimeSetup` (PR2)
- `WindowsRegionLocation` (PR3)
- `WindowsRegionalFormat` (PR4)

Domain enum `WindowsDateTimeApplyMode`: `ManualDateTime = 0`, `TimeZone = 1`, `TimeServer = 2`.

---

## 7. Apply APIs (admin)

| Module | Base path | Ops doc |
|--------|-----------|---------|
| Date & Time Setup (PR2) | `/api/v1/admin/device-config/windows-date-time` | [windows-date-time-setup-operations.md](./windows-date-time-setup-operations.md) |
| Region & Location (PR3) | `/api/v1/admin/device-config/windows-region-location` | [windows-region-location-operations.md](./windows-region-location-operations.md) |
| Regional Format (PR4) | `/api/v1/admin/device-config/windows-regional-format` | [windows-regional-format-operations.md](./windows-regional-format-operations.md) |

---

## 8. Notes

- **v1 Windows `:XP` only** — MAC suffix and agent payloads follow existing Intellinode Windows module conventions.
- **Linux** time/language apply is out of scope for v1.
- **PR1–PR4 complete** the Windows Time & Language apply surface for v1.
