# Intellinode PostgreSQL Database

Single-schema setup for the Intellinode UEM agent–server platform.

## Application data schema (important)

The **C# API uses EF Core** and stores all application data in the **`intellinode` schema** only (`intellinode.devices`, `intellinode.device_inventory`, etc.).

| Source | Schema | Role |
|--------|--------|------|
| **EF Core migrations** (API startup) | `intellinode` | **Primary** — auth, enroll, heartbeat, inventory |
| `intellinode_full_setup.sql` | `intellinode` | Optional — extra PL/pgSQL functions, views, legacy SQL tests |
| Old EF public tables | `public` | **Obsolete** — drop with `cleanup_public_schema.sql` |

On startup, `DatabaseInitializer` runs `MigrateAsync()` and seeds the default tenant, Root group, and admin user.

### Fresh database (recommended)

1. Create database `intellinode`.
2. Start the API — EF applies migration `InitialIntellinodeSchema` to schema `intellinode`.
3. If you previously used public-schema tables, run `database/cleanup_public_schema.sql`.

### Existing database with `intellinode_full_setup.sql` already applied

If Section B of the SQL script already created `intellinode.*` tables, either:

- **Start the API** — `MigrationBootstrapper` creates `intellinode."__EFMigrationsHistory"` and baselines the initial migration automatically, or
- Run in pgAdmin on database **`intellinode`**:

```sql
-- See database/ensure_ef_migrations_history.sql
CREATE SCHEMA IF NOT EXISTS intellinode;

CREATE TABLE IF NOT EXISTS intellinode."__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL PRIMARY KEY,
    product_version character varying(32) NOT NULL
);

INSERT INTO intellinode."__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20260523055049_InitialIntellinodeSchema', '10.0.1')
ON CONFLICT (migration_id) DO NOTHING;
```

Note: with `UseSnakeCaseNamingConvention()`, EF uses columns **`migration_id`** and **`product_version`** (not `MigrationId` / `ProductVersion`).

### Remove obsolete public tables

After confirming the API works against `intellinode.*`:

```powershell
psql -U postgres -h localhost -d intellinode -f database/cleanup_public_schema.sql
```

---

- PostgreSQL **15+**
- pgAdmin **or** `psql` on your PATH
- Superuser access (default: `postgres`)

## Connection

Matches `src/Intellinode.Api/appsettings.json`:

```
Host=localhost;Port=5432;Database=intellinode;Username=postgres;Password=postgres
```

Schema: `intellinode` (all tables, functions, and views live here).

## Run the setup

The script has two sections (A and B). Section A creates the database; Section B creates schema, tables, functions, and seed data. **Do not run the whole file against one database** — objects must be created inside `intellinode`.

### pgAdmin (recommended)

1. Open **Query Tool** connected to database **`postgres`**.
2. Highlight and execute **SECTION A** only (the `CREATE DATABASE` line near the top).
   - Error `42P04 already exists` is OK on re-runs.
3. In the object tree, connect to database **`intellinode`** (create it first via Section A if missing).
4. Open **Query Tool** on **`intellinode`**.
5. Highlight from the line `-- >>> SECTION B` through the end of the file and execute.

### psql (command line)

From the project root (`Intellinode/`), PowerShell:

```powershell
$env:PGPASSWORD = "postgres"
psql -U postgres -h localhost -c "CREATE DATABASE intellinode ENCODING 'UTF8'" 2>$null
$marker = '>>> SECTION B'
(Get-Content "database\intellinode_full_setup.sql" -Raw) -split [regex]::Escape($marker), 2 |
  Select-Object -Last 1 |
  psql -U postgres -h localhost -d intellinode -v ON_ERROR_STOP=1
```

Linux/macOS:

```bash
export PGPASSWORD=postgres
psql -U postgres -h localhost -c "CREATE DATABASE intellinode ENCODING 'UTF8'" 2>/dev/null || true
sed -n '/>>> SECTION B/,$p' database/intellinode_full_setup.sql | psql -U postgres -h localhost -d intellinode -v ON_ERROR_STOP=1
```

Section B is **idempotent**: it uses `IF NOT EXISTS` for schema/objects and `ON CONFLICT DO NOTHING` for seed rows.

## What the script creates

| Step | Contents |
|------|----------|
| Database | `intellinode` (UTF8) |
| Schema | `intellinode` |
| Extensions | `pgcrypto`, `uuid-ossp` |
| Tables | 13 tables (devices, tasks, inventory, admin, etc.) |
| Functions | Legacy FusionX heartbeat/discover procs as PL/pgSQL |
| Views | `vw_device_summary`, `vw_recent_heartbeats`, `vw_pending_tasks` |
| Seed | Default tenant, Root group, admin user |
| Role | `intellinode_app` (password: `change_me`) |

## Default admin login

| Field | Value |
|-------|-------|
| Username | `admin` |
| Password | `Admin@123` |

## Agent command codes

Returned by `fn_process_heartbeat` and related functions (legacy Windows agent contract):

| Code | Meaning |
|------|---------|
| `0` | No pending work |
| `1` | Tasks/commands pending — agent should fetch work |
| `SDFT` | Send Data First Time — upload full inventory |
| `NOK` | Error |

## Quick function tests

After setup, connect to database **`intellinode`** and run:

```sql
SET search_path TO intellinode, public;

-- Register a test device
SELECT fn_register_device(
    '00000000-0000-0000-0000-000000000001'::uuid,
    'AA:BB:CC:DD:EE:FF',
    'TEST-PC'
);

-- First heartbeat → SDFT (no inventory yet)
SELECT fn_process_heartbeat('AA:BB:CC:DD:EE:FF', '01:00:00');

-- Upload inventory, then heartbeat → 0
SELECT fn_upsert_device_inventory(
    fn_get_device_by_mac('00000000-0000-0000-0000-000000000001'::uuid, 'AA:BB:CC:DD:EE:FF'),
    '{"cpu":"Intel"}'::jsonb, '{}'::jsonb, '{}'::jsonb, '{}'::jsonb
);
SELECT fn_process_heartbeat('AA:BB:CC:DD:EE:FF', '01:05:00');

-- Queue a task → heartbeat returns 1
SELECT fn_queue_device_task(
    fn_get_device_by_mac('00000000-0000-0000-0000-000000000001'::uuid, 'AA:BB:CC:DD:EE:FF'),
    'Shutdown', 'Shutdown', '', 42
);
SELECT fn_process_heartbeat('AA:BB:CC:DD:EE:FF', '01:10:00');
```

## FusionX → PostgreSQL mapping

| Legacy FusionX | PostgreSQL function |
|----------------|---------------------|
| `OnlyHeartBitproc_TCS` | `fn_process_heartbeat` |
| `OnlyHeartBitManageAck_TCS_Windows` | `fn_process_heartbeat_ack` |
| `PRC_HBT_Details` | `fn_heartbeat_binding_ip` |
| `PRC_HBT_Details_HostName` | `fn_heartbeat_binding_hostname` |
| `XP_prcUpdateIPAddress` | `fn_update_device_ip_address` |
| `CheckDiscoverLookupEntry` | `fn_check_discover_lookup` |

## App connection string tip

Add schema search path for direct SQL/ORM access:

```
Host=localhost;Port=5432;Database=intellinode;Username=postgres;Password=postgres;Search Path=intellinode,public
```

For production, use the `intellinode_app` role and change its password after setup:

```sql
ALTER ROLE intellinode_app WITH PASSWORD 'your_secure_password';
```
