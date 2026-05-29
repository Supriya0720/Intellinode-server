-- ============================================================
-- Intellinode UEM — Full Database Setup (single script)
-- PostgreSQL 15+
--
-- pgAdmin:
--   1. Connect to database "postgres" → highlight & run SECTION A only.
--   2. Connect to database "intellinode" → highlight & run SECTION B only.
--
-- psql (PowerShell — see database/README.md):
--   Creates the database, then runs SECTION B against intellinode.
-- ============================================================

-- ============================================================
-- SECTION A — connect to database "postgres", run this block
-- ============================================================
CREATE DATABASE intellinode ENCODING 'UTF8';
-- If you see "already exists" (SQLSTATE 42P04), that is fine — continue to SECTION B.

-- >>> SECTION B — connect to database "intellinode", run from here to end of file <<<

SET client_encoding = 'UTF8';
SET search_path TO intellinode, public;

-- ============================================================
-- Schema
-- ============================================================
CREATE SCHEMA IF NOT EXISTS intellinode;

-- ============================================================
-- Step 4: Extensions
-- ============================================================
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ============================================================
-- Step 5: Custom types
-- ============================================================
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace
                   WHERE t.typname = 'enrollment_state' AND n.nspname = 'intellinode') THEN
        CREATE TYPE intellinode.enrollment_state AS ENUM (
            'PendingInventory', 'Active', 'Unlicensed', 'Disabled',
            'PendingApproval', 'Rejected'
        );
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace
                   WHERE t.typname = 'discover_lookup_status' AND n.nspname = 'intellinode') THEN
        CREATE TYPE intellinode.discover_lookup_status AS ENUM ('Pending', 'Approved', 'Rejected');
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace
                   WHERE t.typname = 'heartbeat_binding_kind' AND n.nspname = 'intellinode') THEN
        CREATE TYPE intellinode.heartbeat_binding_kind AS ENUM ('IpAddress', 'HostName');
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace
                   WHERE t.typname = 'ip_update_result' AND n.nspname = 'intellinode') THEN
        CREATE TYPE intellinode.ip_update_result AS (
            update_status VARCHAR(16),
            host_name     VARCHAR(255),
            client_status VARCHAR(16)
        );
    END IF;
END $$;

ALTER TYPE intellinode.enrollment_state ADD VALUE IF NOT EXISTS 'PendingApproval';
ALTER TYPE intellinode.enrollment_state ADD VALUE IF NOT EXISTS 'Rejected';

-- ============================================================
-- Step 6: Tables
-- ============================================================

CREATE TABLE IF NOT EXISTS intellinode.tenants (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name        VARCHAR(200) NOT NULL,
    host_name   VARCHAR(255),
    is_active   BOOLEAN NOT NULL DEFAULT TRUE,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS intellinode.device_groups (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id        UUID NOT NULL REFERENCES intellinode.tenants(id),
    parent_group_id  UUID REFERENCES intellinode.device_groups(id),
    name             VARCHAR(200) NOT NULL,
    sort_order       INT NOT NULL DEFAULT 0,
    is_default       BOOLEAN NOT NULL DEFAULT FALSE,
    created_utc      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- When device_groups already exists, CREATE TABLE is skipped; apply hierarchy columns/indexes here.
ALTER TABLE intellinode.device_groups
    ADD COLUMN IF NOT EXISTS parent_group_id UUID;
ALTER TABLE intellinode.device_groups
    ADD COLUMN IF NOT EXISTS sort_order INT NOT NULL DEFAULT 0;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fk_device_groups_device_groups_parent_group_id'
    ) THEN
        ALTER TABLE intellinode.device_groups
            ADD CONSTRAINT fk_device_groups_device_groups_parent_group_id
            FOREIGN KEY (parent_group_id) REFERENCES intellinode.device_groups(id);
    END IF;
END $$;

DROP INDEX IF EXISTS intellinode.ix_device_groups_tenant_id_name;
DROP INDEX IF EXISTS intellinode.ix_device_groups_tenant_id_name_root;
ALTER TABLE intellinode.device_groups
    DROP CONSTRAINT IF EXISTS device_groups_tenant_id_name_key;

CREATE UNIQUE INDEX IF NOT EXISTS ix_device_groups_tenant_id_name
    ON intellinode.device_groups (tenant_id, name)
    WHERE parent_group_id IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ix_device_groups_tenant_id_parent_group_id_name
    ON intellinode.device_groups (tenant_id, parent_group_id, name)
    WHERE parent_group_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_device_groups_parent_group_id
    ON intellinode.device_groups (parent_group_id);

CREATE TABLE IF NOT EXISTS intellinode.devices (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id                UUID NOT NULL REFERENCES intellinode.tenants(id),
    mac_address              VARCHAR(300) NOT NULL,
    host_name                VARCHAR(255) NOT NULL DEFAULT '',
    ip_address               VARCHAR(64) NOT NULL DEFAULT '',
    communication_ip_address TEXT NOT NULL DEFAULT '',
    subnet_mask              TEXT NOT NULL DEFAULT '',
    gateway                  TEXT NOT NULL DEFAULT '',
    primary_dns              TEXT NOT NULL DEFAULT '',
    secondary_dns            TEXT NOT NULL DEFAULT '',
    primary_wins             TEXT NOT NULL DEFAULT '',
    secondary_wins           TEXT NOT NULL DEFAULT '',
    domain                   TEXT NOT NULL DEFAULT '',
    workgroup                TEXT NOT NULL DEFAULT '',
    login_user_name          TEXT NOT NULL DEFAULT '',
    user_name                TEXT NOT NULL DEFAULT '',
    license_key              TEXT NOT NULL DEFAULT '',
    communication_type       VARCHAR(32) NOT NULL DEFAULT 'HTTP',
    agent_up_time            VARCHAR(64) NOT NULL DEFAULT '',
    duration                 VARCHAR(64) NOT NULL DEFAULT '',
    poll_interval            INT NOT NULL DEFAULT 300,
    is_dhcp                  BOOLEAN NOT NULL DEFAULT FALSE,
    is_domain_joined         BOOLEAN NOT NULL DEFAULT FALSE,
    is_online                BOOLEAN NOT NULL DEFAULT FALSE,
    is_service_mode          BOOLEAN NOT NULL DEFAULT FALSE,
    is_licensed              BOOLEAN NOT NULL DEFAULT TRUE,
    is_registered            BOOLEAN NOT NULL DEFAULT FALSE,
    enrollment_state         intellinode.enrollment_state NOT NULL DEFAULT 'PendingInventory',
    client_status            VARCHAR(32) NOT NULL DEFAULT 'OFF',
    os                       VARCHAR(64) NOT NULL DEFAULT 'Windows',
    os_version               VARCHAR(64) NOT NULL DEFAULT '',
    agent_version            VARCHAR(64) NOT NULL DEFAULT '',
    group_id                 UUID REFERENCES intellinode.device_groups(id),
    last_heartbeat_utc       TIMESTAMPTZ,
    created_utc              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_utc              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (tenant_id, mac_address)
);

CREATE TABLE IF NOT EXISTS intellinode.device_status (
    device_id          UUID PRIMARY KEY REFERENCES intellinode.devices(id) ON DELETE CASCADE,
    last_ip            VARCHAR(64),
    logged_in_user     VARCHAR(256),
    uptime_minutes     INT,
    is_online          BOOLEAN NOT NULL DEFAULT FALSE,
    last_client_status VARCHAR(32),
    updated_utc        TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS intellinode.agent_refresh_tokens (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id   UUID NOT NULL REFERENCES intellinode.devices(id) ON DELETE CASCADE,
    token_hash  TEXT NOT NULL,
    expires_utc TIMESTAMPTZ NOT NULL,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    revoked_utc TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS intellinode.agent_enrollment_tokens (
    id                   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    token_hash           TEXT NOT NULL UNIQUE,
    mac_address          VARCHAR(300),
    device_id            UUID REFERENCES intellinode.devices(id) ON DELETE SET NULL,
    created_by_admin_id  UUID REFERENCES intellinode.admin_users(id) ON DELETE SET NULL,
    expires_utc          TIMESTAMPTZ NOT NULL,
    consumed_utc         TIMESTAMPTZ,
    created_utc          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS intellinode.device_tasks (
    id                 UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id          UUID NOT NULL REFERENCES intellinode.devices(id) ON DELETE CASCADE,
    legacy_task_id     INT NOT NULL DEFAULT 0,
    module_name        VARCHAR(128) NOT NULL DEFAULT '',
    function_name      VARCHAR(128) NOT NULL DEFAULT '',
    function_parameter VARCHAR(512) NOT NULL DEFAULT '',
    extra_data         VARCHAR(512) NOT NULL DEFAULT '',
    status             INT NOT NULL DEFAULT 0,
    created_utc        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_utc      TIMESTAMPTZ,
    CONSTRAINT ck_device_tasks_status CHECK (status BETWEEN 0 AND 3)
);

CREATE TABLE IF NOT EXISTS intellinode.heartbeat_binding_changes (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id         UUID NOT NULL REFERENCES intellinode.devices(id) ON DELETE CASCADE,
    is_service_mode   BOOLEAN NOT NULL DEFAULT FALSE,
    status            VARCHAR(32) NOT NULL,
    changed_value     VARCHAR(512) NOT NULL,
    kind              intellinode.heartbeat_binding_kind NOT NULL DEFAULT 'IpAddress',
    is_binding_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_utc       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS intellinode.device_inventory (
    device_id     UUID PRIMARY KEY REFERENCES intellinode.devices(id) ON DELETE CASCADE,
    hardware      JSONB,
    network       JSONB,
    os_info       JSONB,
    security      JSONB,
    collected_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    version       INT NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS intellinode.device_network_history (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id     UUID NOT NULL REFERENCES intellinode.devices(id) ON DELETE CASCADE,
    old_ip        VARCHAR(64),
    new_ip        VARCHAR(64),
    old_host_name VARCHAR(255),
    new_host_name VARCHAR(255),
    is_dhcp       BOOLEAN,
    change_source VARCHAR(32) NOT NULL DEFAULT 'Client',
    changed_utc   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS intellinode.discover_lookup (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id             UUID NOT NULL REFERENCES intellinode.tenants(id) ON DELETE CASCADE,
    device_id             UUID REFERENCES intellinode.devices(id) ON DELETE SET NULL,
    mac_address           VARCHAR(300) NOT NULL,
    host_name             VARCHAR(255) NOT NULL DEFAULT '',
    ip_address            VARCHAR(64) NOT NULL DEFAULT '',
    domain                VARCHAR(255) NOT NULL DEFAULT '',
    os_name               VARCHAR(64) NOT NULL DEFAULT '',
    os_version            VARCHAR(64) NOT NULL DEFAULT '',
    agent_version         VARCHAR(64) NOT NULL DEFAULT '',
    discovery_type        VARCHAR(64) NOT NULL DEFAULT 'AgentSelfDiscovery',
    status                intellinode.discover_lookup_status NOT NULL DEFAULT 'Pending',
    discovered_utc        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_utc           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    approved_by_admin_id  UUID,
    approved_utc          TIMESTAMPTZ,
    rejected_by_admin_id  UUID,
    rejected_utc          TIMESTAMPTZ,
    rejection_reason      VARCHAR(500),
    notes                 VARCHAR(1000),
    UNIQUE (tenant_id, mac_address)
);

ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS device_id UUID;
ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS host_name VARCHAR(255) NOT NULL DEFAULT '';
ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS ip_address VARCHAR(64) NOT NULL DEFAULT '';
ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS domain VARCHAR(255) NOT NULL DEFAULT '';
ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS os_name VARCHAR(64) NOT NULL DEFAULT '';
ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS os_version VARCHAR(64) NOT NULL DEFAULT '';
ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS agent_version VARCHAR(64) NOT NULL DEFAULT '';
ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS discovery_type VARCHAR(64) NOT NULL DEFAULT 'AgentSelfDiscovery';
ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS discovered_utc TIMESTAMPTZ NOT NULL DEFAULT NOW();
ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS approved_by_admin_id UUID;
ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS approved_utc TIMESTAMPTZ;
ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS rejected_by_admin_id UUID;
ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS rejected_utc TIMESTAMPTZ;
ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS rejection_reason VARCHAR(500);
ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS notes VARCHAR(1000);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'intellinode' AND table_name = 'discover_lookup' AND column_name = 'status'
    ) THEN
        ALTER TABLE intellinode.discover_lookup
            ADD COLUMN status intellinode.discover_lookup_status NOT NULL DEFAULT 'Pending';
    END IF;
END $$;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'intellinode' AND table_name = 'discover_lookup' AND column_name = 'created_utc'
    ) THEN
        UPDATE intellinode.discover_lookup
           SET discovered_utc = created_utc
         WHERE discovered_utc IS NULL;
    END IF;
END $$;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'intellinode' AND table_name = 'discover_lookup' AND column_name = 'lookup_status'
    ) THEN
        UPDATE intellinode.discover_lookup
           SET status = CASE lookup_status
               WHEN 'Registered' THEN 'Approved'::intellinode.discover_lookup_status
               WHEN 'Rejected' THEN 'Rejected'::intellinode.discover_lookup_status
               ELSE 'Pending'::intellinode.discover_lookup_status
           END;
        ALTER TABLE intellinode.discover_lookup DROP COLUMN lookup_status;
    END IF;
END $$;

ALTER TABLE intellinode.discover_lookup DROP COLUMN IF EXISTS created_utc;

CREATE TABLE IF NOT EXISTS intellinode.agent_communication_logs (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id       UUID REFERENCES intellinode.devices(id) ON DELETE SET NULL,
    mac_address     VARCHAR(300),
    direction       VARCHAR(16) NOT NULL,
    endpoint        VARCHAR(256) NOT NULL,
    payload_summary TEXT,
    command_code    VARCHAR(16),
    created_utc     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS intellinode.exception_logs (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source       VARCHAR(256) NOT NULL,
    message      TEXT NOT NULL,
    stack_trace  TEXT,
    request_path VARCHAR(512),
    http_method  VARCHAR(16),
    device_id    UUID,
    admin_id     UUID,
    logged_utc   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS intellinode.admin_users (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_name     VARCHAR(100) NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    display_name  VARCHAR(200) NOT NULL DEFAULT '',
    is_active     BOOLEAN NOT NULL DEFAULT TRUE,
    created_utc   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS intellinode.admin_sessions (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    admin_user_id UUID NOT NULL REFERENCES intellinode.admin_users(id) ON DELETE CASCADE,
    jwt_id        VARCHAR(64) NOT NULL UNIQUE,
    expires_utc   TIMESTAMPTZ NOT NULL,
    revoked_utc   TIMESTAMPTZ,
    created_utc   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_discover_lookup_admin_users_approved_by_admin_id'
    ) THEN
        ALTER TABLE intellinode.discover_lookup
            ADD CONSTRAINT fk_discover_lookup_admin_users_approved_by_admin_id
            FOREIGN KEY (approved_by_admin_id) REFERENCES intellinode.admin_users(id) ON DELETE SET NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_discover_lookup_admin_users_rejected_by_admin_id'
    ) THEN
        ALTER TABLE intellinode.discover_lookup
            ADD CONSTRAINT fk_discover_lookup_admin_users_rejected_by_admin_id
            FOREIGN KEY (rejected_by_admin_id) REFERENCES intellinode.admin_users(id) ON DELETE SET NULL;
    END IF;
END $$;

-- ============================================================
-- Step 7: Indexes
-- ============================================================
CREATE INDEX IF NOT EXISTS ix_devices_tenant_id
    ON intellinode.devices (tenant_id);

CREATE INDEX IF NOT EXISTS ix_devices_mac_address
    ON intellinode.devices (mac_address);

CREATE INDEX IF NOT EXISTS ix_devices_last_heartbeat_utc
    ON intellinode.devices (last_heartbeat_utc);

CREATE INDEX IF NOT EXISTS ix_devices_enrollment_state
    ON intellinode.devices (enrollment_state);

CREATE INDEX IF NOT EXISTS ix_agent_enrollment_tokens_token_hash
    ON intellinode.agent_enrollment_tokens (token_hash);

CREATE INDEX IF NOT EXISTS ix_agent_enrollment_tokens_expires_utc
    ON intellinode.agent_enrollment_tokens (expires_utc);

CREATE INDEX IF NOT EXISTS ix_devices_group_id
    ON intellinode.devices (group_id);

CREATE INDEX IF NOT EXISTS ix_device_tasks_device_id
    ON intellinode.device_tasks (device_id);

CREATE INDEX IF NOT EXISTS ix_device_tasks_device_id_status
    ON intellinode.device_tasks (device_id, status);

CREATE INDEX IF NOT EXISTS ix_agent_refresh_tokens_device_id
    ON intellinode.agent_refresh_tokens (device_id);

CREATE INDEX IF NOT EXISTS ix_agent_refresh_tokens_token_hash
    ON intellinode.agent_refresh_tokens (token_hash);

CREATE INDEX IF NOT EXISTS ix_heartbeat_binding_changes_device_id
    ON intellinode.heartbeat_binding_changes (device_id);

CREATE INDEX IF NOT EXISTS ix_discover_lookup_tenant_id_mac_address
    ON intellinode.discover_lookup (tenant_id, mac_address);

CREATE INDEX IF NOT EXISTS ix_discover_lookup_tenant_id_status_discovered_utc
    ON intellinode.discover_lookup (tenant_id, status, discovered_utc);

CREATE INDEX IF NOT EXISTS ix_agent_communication_logs_device_id
    ON intellinode.agent_communication_logs (device_id, created_utc DESC);

CREATE INDEX IF NOT EXISTS ix_exception_logs_logged_utc
    ON intellinode.exception_logs (logged_utc DESC);

-- ============================================================
-- Step 8: Triggers (updated_utc)
-- ============================================================
CREATE OR REPLACE FUNCTION intellinode.trg_set_updated_utc()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.updated_utc := NOW();
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_devices_updated_utc ON intellinode.devices;
CREATE TRIGGER trg_devices_updated_utc
    BEFORE UPDATE ON intellinode.devices
    FOR EACH ROW
    EXECUTE FUNCTION intellinode.trg_set_updated_utc();

DROP TRIGGER IF EXISTS trg_discover_lookup_updated_utc ON intellinode.discover_lookup;
CREATE TRIGGER trg_discover_lookup_updated_utc
    BEFORE UPDATE ON intellinode.discover_lookup
    FOR EACH ROW
    EXECUTE FUNCTION intellinode.trg_set_updated_utc();

-- ============================================================
-- Step 9: PostgreSQL functions (FusionX legacy replacements)
-- ============================================================

-- Helper: resolve device UUID by tenant + MAC
CREATE OR REPLACE FUNCTION intellinode.fn_get_device_by_mac(
    p_tenant_id   UUID,
    p_mac_address VARCHAR
)
RETURNS UUID
LANGUAGE plpgsql
STABLE
SET search_path = intellinode, public
AS $$
DECLARE
    v_device_id UUID;
BEGIN
    SELECT id
      INTO v_device_id
      FROM intellinode.devices
     WHERE tenant_id = p_tenant_id
       AND UPPER(TRIM(mac_address)) = UPPER(TRIM(p_mac_address))
     LIMIT 1;

    RETURN v_device_id;
END;
$$;

COMMENT ON FUNCTION intellinode.fn_get_device_by_mac(UUID, VARCHAR) IS
    'Returns device UUID for a tenant/MAC pair.';

-- Replaces FusionX CheckDiscoverLookupEntry
CREATE OR REPLACE FUNCTION intellinode.fn_check_discover_lookup(
    p_mac_address VARCHAR,
    p_tenant_id   UUID DEFAULT '00000000-0000-0000-0000-000000000001'::UUID
)
RETURNS VARCHAR
LANGUAGE plpgsql
STABLE
SET search_path = intellinode, public
AS $$
DECLARE
    v_device intellinode.devices%ROWTYPE;
BEGIN
    SELECT *
      INTO v_device
      FROM intellinode.devices
     WHERE tenant_id = p_tenant_id
       AND UPPER(TRIM(mac_address)) = UPPER(TRIM(p_mac_address))
     LIMIT 1;

    IF NOT FOUND THEN
        RETURN 'SDFT';
    END IF;

    IF NOT v_device.is_registered
       OR v_device.enrollment_state = 'PendingInventory'
       OR NOT EXISTS (
           SELECT 1
             FROM intellinode.device_inventory di
            WHERE di.device_id = v_device.id
       ) THEN
        RETURN 'SDFT';
    END IF;

    RETURN 'exists';
END;
$$;

COMMENT ON FUNCTION intellinode.fn_check_discover_lookup(VARCHAR, UUID) IS
    'Legacy FusionX CheckDiscoverLookupEntry — returns SDFT for new/unregistered devices, else exists.';

-- Replaces FusionX PRC_HBT_Details
CREATE OR REPLACE FUNCTION intellinode.fn_heartbeat_binding_ip(
    p_mac_address  VARCHAR,
    p_sm_status    BOOLEAN,
    p_status       VARCHAR,
    p_str_change   VARCHAR,
    p_tenant_id    UUID DEFAULT '00000000-0000-0000-0000-000000000001'::UUID
)
RETURNS BOOLEAN
LANGUAGE plpgsql
SET search_path = intellinode, public
AS $$
DECLARE
    v_device_id UUID;
    v_device    intellinode.devices%ROWTYPE;
    v_existing  intellinode.heartbeat_binding_changes%ROWTYPE;
BEGIN
    v_device_id := intellinode.fn_get_device_by_mac(p_tenant_id, p_mac_address);
    IF v_device_id IS NULL THEN
        RETURN FALSE;
    END IF;

    SELECT * INTO v_device FROM intellinode.devices WHERE id = v_device_id;

    SELECT *
      INTO v_existing
      FROM intellinode.heartbeat_binding_changes
     WHERE device_id = v_device_id
       AND is_binding_active = TRUE
     ORDER BY created_utc DESC
     LIMIT 1;

    IF FOUND
       AND v_existing.status = p_status
       AND v_existing.changed_value = p_str_change
       AND v_existing.is_service_mode = p_sm_status
       AND v_existing.kind = 'IpAddress'::intellinode.heartbeat_binding_kind THEN
        RETURN TRUE;
    END IF;

    IF UPPER(TRIM(v_device.ip_address)) = UPPER(TRIM(p_str_change)) THEN
        RETURN FALSE;
    END IF;

    IF FOUND THEN
        UPDATE intellinode.heartbeat_binding_changes
           SET is_binding_active = FALSE
         WHERE id = v_existing.id;
    END IF;

    INSERT INTO intellinode.heartbeat_binding_changes (
        device_id, is_service_mode, status, changed_value, kind, is_binding_active
    ) VALUES (
        v_device_id, p_sm_status, p_status, p_str_change, 'IpAddress', TRUE
    );

    RETURN TRUE;
END;
$$;

COMMENT ON FUNCTION intellinode.fn_heartbeat_binding_ip(VARCHAR, BOOLEAN, VARCHAR, VARCHAR, UUID) IS
    'Legacy FusionX PRC_HBT_Details — multi-NIC IP binding change detection. TRUE = binding still active.';

-- Replaces FusionX PRC_HBT_Details_HostName
CREATE OR REPLACE FUNCTION intellinode.fn_heartbeat_binding_hostname(
    p_mac_address  VARCHAR,
    p_sm_status    BOOLEAN,
    p_status       VARCHAR,
    p_str_change   VARCHAR,
    p_tenant_id    UUID DEFAULT '00000000-0000-0000-0000-000000000001'::UUID
)
RETURNS BOOLEAN
LANGUAGE plpgsql
SET search_path = intellinode, public
AS $$
DECLARE
    v_device_id UUID;
    v_device    intellinode.devices%ROWTYPE;
    v_existing  intellinode.heartbeat_binding_changes%ROWTYPE;
BEGIN
    v_device_id := intellinode.fn_get_device_by_mac(p_tenant_id, p_mac_address);
    IF v_device_id IS NULL THEN
        RETURN FALSE;
    END IF;

    SELECT * INTO v_device FROM intellinode.devices WHERE id = v_device_id;

    SELECT *
      INTO v_existing
      FROM intellinode.heartbeat_binding_changes
     WHERE device_id = v_device_id
       AND is_binding_active = TRUE
     ORDER BY created_utc DESC
     LIMIT 1;

    IF FOUND
       AND v_existing.status = p_status
       AND v_existing.changed_value = p_str_change
       AND v_existing.is_service_mode = p_sm_status
       AND v_existing.kind = 'HostName'::intellinode.heartbeat_binding_kind THEN
        RETURN TRUE;
    END IF;

    IF UPPER(TRIM(v_device.host_name)) = UPPER(TRIM(p_str_change)) THEN
        RETURN FALSE;
    END IF;

    IF FOUND THEN
        UPDATE intellinode.heartbeat_binding_changes
           SET is_binding_active = FALSE
         WHERE id = v_existing.id;
    END IF;

    INSERT INTO intellinode.heartbeat_binding_changes (
        device_id, is_service_mode, status, changed_value, kind, is_binding_active
    ) VALUES (
        v_device_id, p_sm_status, p_status, p_str_change, 'HostName', TRUE
    );

    RETURN TRUE;
END;
$$;

COMMENT ON FUNCTION intellinode.fn_heartbeat_binding_hostname(VARCHAR, BOOLEAN, VARCHAR, VARCHAR, UUID) IS
    'Legacy FusionX PRC_HBT_Details_HostName — single-NIC hostname binding change detection.';

-- Replaces FusionX XP_prcUpdateIPAddress
CREATE OR REPLACE FUNCTION intellinode.fn_update_device_ip_address(
    p_mac_address VARCHAR,
    p_ip_address  VARCHAR,
    p_is_dhcp     BOOLEAN DEFAULT FALSE,
    p_from_client BOOLEAN DEFAULT TRUE,
    p_tenant_id   UUID DEFAULT '00000000-0000-0000-0000-000000000001'::UUID
)
RETURNS intellinode.ip_update_result
LANGUAGE plpgsql
SET search_path = intellinode, public
AS $$
DECLARE
    v_device_id     UUID;
    v_device        intellinode.devices%ROWTYPE;
    v_result        intellinode.ip_update_result;
    v_client_status VARCHAR(16) := 'SAME';
BEGIN
    v_device_id := intellinode.fn_get_device_by_mac(p_tenant_id, p_mac_address);
    IF v_device_id IS NULL THEN
        RAISE EXCEPTION 'Device not found for MAC %', p_mac_address USING ERRCODE = 'P0002';
    END IF;

    SELECT * INTO v_device FROM intellinode.devices WHERE id = v_device_id FOR UPDATE;

    v_result.host_name := v_device.host_name;

    IF UPPER(TRIM(v_device.ip_address)) <> UPPER(TRIM(p_ip_address)) THEN
        INSERT INTO intellinode.device_network_history (
            device_id, old_ip, new_ip, old_host_name, new_host_name, is_dhcp, change_source
        ) VALUES (
            v_device_id,
            NULLIF(v_device.ip_address, ''),
            p_ip_address,
            NULLIF(v_device.host_name, ''),
            NULLIF(v_device.host_name, ''),
            p_is_dhcp,
            CASE WHEN p_from_client THEN 'Client' ELSE 'Server' END
        );

        UPDATE intellinode.devices
           SET ip_address = p_ip_address,
               is_dhcp = p_is_dhcp,
               updated_utc = NOW()
         WHERE id = v_device_id;

        v_client_status := CASE WHEN p_from_client THEN 'CHANGE' ELSE 'UPDATE' END;
        v_result.update_status := 'Update';
    ELSE
        v_result.update_status := 'NoUpdate';
    END IF;

    IF COALESCE(v_result.host_name, '') = '' THEN
        v_result.host_name := v_device.host_name;
    END IF;

    v_result.client_status := v_client_status;

    UPDATE intellinode.device_status
       SET last_ip = p_ip_address,
           is_online = p_from_client,
           updated_utc = NOW()
     WHERE device_id = v_device_id;

    IF NOT FOUND THEN
        INSERT INTO intellinode.device_status (device_id, last_ip, is_online)
        VALUES (v_device_id, p_ip_address, p_from_client);
    END IF;

    RETURN v_result;
END;
$$;

COMMENT ON FUNCTION intellinode.fn_update_device_ip_address(VARCHAR, VARCHAR, BOOLEAN, BOOLEAN, UUID) IS
    'Legacy FusionX XP_prcUpdateIPAddress — updates device IP and records network history.';

-- Supporting: complete a device task (defined before ack/heartbeat callers)
CREATE OR REPLACE FUNCTION intellinode.fn_complete_device_task(
    p_device_id      UUID,
    p_legacy_task_id INT DEFAULT 0,
    p_module_name    VARCHAR DEFAULT NULL,
    p_function_name  VARCHAR DEFAULT NULL
)
RETURNS VOID
LANGUAGE plpgsql
SET search_path = intellinode, public
AS $$
BEGIN
    UPDATE intellinode.device_tasks
       SET status = 2,
           completed_utc = NOW()
     WHERE device_id = p_device_id
       AND status IN (0, 1)
       AND (p_legacy_task_id = 0 OR legacy_task_id = p_legacy_task_id)
       AND (p_module_name IS NULL OR module_name = p_module_name)
       AND (p_function_name IS NULL OR function_name = p_function_name);
END;
$$;

COMMENT ON FUNCTION intellinode.fn_complete_device_task(UUID, INT, VARCHAR, VARCHAR) IS
    'Marks matching pending/in-process tasks as completed.';

-- Replaces FusionX OnlyHeartBitManageAck_TCS_Windows
CREATE OR REPLACE FUNCTION intellinode.fn_process_heartbeat_ack(
    p_mac_address   VARCHAR,
    p_shutdown_ack  VARCHAR DEFAULT '',
    p_state         VARCHAR DEFAULT '',
    p_task_id       INT DEFAULT 0,
    p_tenant_id     UUID DEFAULT '00000000-0000-0000-0000-000000000001'::UUID
)
RETURNS VARCHAR
LANGUAGE plpgsql
SET search_path = intellinode, public
AS $$
DECLARE
    v_device_id UUID;
    v_now       TIMESTAMPTZ := NOW();
    v_state     VARCHAR := UPPER(TRIM(COALESCE(p_state, '')));
    v_ack       VARCHAR := UPPER(TRIM(COALESCE(p_shutdown_ack, '')));
BEGIN
    v_device_id := intellinode.fn_get_device_by_mac(p_tenant_id, p_mac_address);
    IF v_device_id IS NULL THEN
        RETURN 'NOK';
    END IF;

    BEGIN
        IF v_state = 'COFF' THEN
            UPDATE intellinode.devices
               SET is_online = FALSE,
                   client_status = 'COFF',
                   last_heartbeat_utc = v_now,
                   updated_utc = v_now
             WHERE id = v_device_id;

            UPDATE intellinode.device_status
               SET is_online = FALSE,
                   last_client_status = 'COFF',
                   updated_utc = v_now
             WHERE device_id = v_device_id;

            IF p_task_id > 0 THEN
                PERFORM intellinode.fn_complete_device_task(v_device_id, p_task_id, NULL, NULL);
            END IF;
            RETURN '0';
        END IF;

        IF v_ack = 'SH' THEN
            UPDATE intellinode.devices
               SET is_online = FALSE,
                   client_status = 'OFF',
                   last_heartbeat_utc = v_now,
                   updated_utc = v_now
             WHERE id = v_device_id;

            PERFORM intellinode.fn_complete_device_task(
                v_device_id, p_task_id, NULL, 'Shutdown'
            );

            INSERT INTO intellinode.agent_communication_logs (
                device_id, mac_address, direction, endpoint, command_code, payload_summary
            ) VALUES (
                v_device_id, p_mac_address, 'Inbound', 'fn_process_heartbeat_ack', '1', 'Shutdown ack'
            );

            RETURN '1';
        END IF;

        IF v_ack = 'RT' THEN
            UPDATE intellinode.devices
               SET is_online = (v_state = 'ON'),
                   client_status = CASE WHEN v_state IN ('ON', 'OFF', 'COFF') THEN v_state ELSE client_status END,
                   last_heartbeat_utc = v_now,
                   updated_utc = v_now
             WHERE id = v_device_id;

            PERFORM intellinode.fn_complete_device_task(
                v_device_id, p_task_id, NULL, 'Restart'
            );

            INSERT INTO intellinode.agent_communication_logs (
                device_id, mac_address, direction, endpoint, command_code, payload_summary
            ) VALUES (
                v_device_id, p_mac_address, 'Inbound', 'fn_process_heartbeat_ack', '1', 'Restart ack'
            );

            RETURN '1';
        END IF;

        UPDATE intellinode.devices
           SET last_heartbeat_utc = v_now,
               updated_utc = v_now
         WHERE id = v_device_id;

        RETURN '0';
    EXCEPTION
        WHEN OTHERS THEN
            RETURN 'NOK';
    END;
END;
$$;

COMMENT ON FUNCTION intellinode.fn_process_heartbeat_ack(VARCHAR, VARCHAR, VARCHAR, INT, UUID) IS
    'Legacy FusionX OnlyHeartBitManageAck_TCS_Windows — COFF/SH/RT acknowledgment path.';

-- Replaces FusionX OnlyHeartBitproc_TCS
CREATE OR REPLACE FUNCTION intellinode.fn_process_heartbeat(
    p_mac_address   VARCHAR,
    p_agent_up_time VARCHAR DEFAULT '',
    p_tenant_id     UUID DEFAULT '00000000-0000-0000-0000-000000000001'::UUID
)
RETURNS VARCHAR
LANGUAGE plpgsql
SET search_path = intellinode, public
AS $$
DECLARE
    v_device_id   UUID;
    v_device      intellinode.devices%ROWTYPE;
    v_now         TIMESTAMPTZ := NOW();
    v_pending     INT;
    v_discover    VARCHAR;
BEGIN
    v_device_id := intellinode.fn_get_device_by_mac(p_tenant_id, p_mac_address);

    IF v_device_id IS NULL THEN
        RETURN 'SDFT';
    END IF;

    BEGIN
        SELECT * INTO v_device FROM intellinode.devices WHERE id = v_device_id FOR UPDATE;

        UPDATE intellinode.devices
           SET agent_up_time = COALESCE(NULLIF(TRIM(p_agent_up_time), ''), agent_up_time),
               last_heartbeat_utc = v_now,
               is_online = TRUE,
               updated_utc = v_now
         WHERE id = v_device_id;

        INSERT INTO intellinode.device_status (device_id, is_online, uptime_minutes, updated_utc)
        VALUES (v_device_id, TRUE, NULL, v_now)
        ON CONFLICT (device_id) DO UPDATE
           SET is_online = TRUE,
               updated_utc = v_now;

        IF NOT v_device.is_registered
           OR v_device.enrollment_state = 'PendingInventory'
           OR NOT EXISTS (
               SELECT 1 FROM intellinode.device_inventory di WHERE di.device_id = v_device_id
           ) THEN
            INSERT INTO intellinode.agent_communication_logs (
                device_id, mac_address, direction, endpoint, command_code, payload_summary
            ) VALUES (
                v_device_id, p_mac_address, 'Inbound', 'fn_process_heartbeat', 'SDFT', 'Inventory required'
            );
            RETURN 'SDFT';
        END IF;

        SELECT COUNT(*)
          INTO v_pending
          FROM intellinode.device_tasks
         WHERE device_id = v_device_id
           AND status IN (0, 1);

        IF v_pending > 0 THEN
            INSERT INTO intellinode.agent_communication_logs (
                device_id, mac_address, direction, endpoint, command_code, payload_summary
            ) VALUES (
                v_device_id, p_mac_address, 'Inbound', 'fn_process_heartbeat', '1',
                format('%s pending task(s)', v_pending)
            );
            RETURN '1';
        END IF;

        IF EXISTS (
            SELECT 1
              FROM intellinode.device_tasks
             WHERE device_id = v_device_id
               AND (function_name = 'Get_FBWF_UWF_Status' OR status = 1)
        ) THEN
            RETURN '1';
        END IF;

        v_discover := intellinode.fn_check_discover_lookup(p_mac_address, p_tenant_id);
        IF v_discover = 'SDFT' THEN
            RETURN 'SDFT';
        END IF;

        INSERT INTO intellinode.agent_communication_logs (
            device_id, mac_address, direction, endpoint, command_code, payload_summary
        ) VALUES (
            v_device_id, p_mac_address, 'Inbound', 'fn_process_heartbeat', '0', 'No pending work'
        );

        RETURN '0';
    EXCEPTION
        WHEN OTHERS THEN
            RETURN 'NOK';
    END;
END;
$$;

COMMENT ON FUNCTION intellinode.fn_process_heartbeat(VARCHAR, VARCHAR, UUID) IS
    'Legacy FusionX OnlyHeartBitproc_TCS — normal heartbeat; returns agent command code 0/1/SDFT/NOK.';

-- Supporting: register a new device
CREATE OR REPLACE FUNCTION intellinode.fn_register_device(
    p_tenant_id     UUID,
    p_mac_address   VARCHAR,
    p_host_name     VARCHAR DEFAULT '',
    p_os            VARCHAR DEFAULT 'Windows',
    p_os_version    VARCHAR DEFAULT '',
    p_agent_version VARCHAR DEFAULT ''
)
RETURNS UUID
LANGUAGE plpgsql
SET search_path = intellinode, public
AS $$
DECLARE
    v_device_id  UUID;
    v_group_id   UUID;
BEGIN
    v_device_id := intellinode.fn_get_device_by_mac(p_tenant_id, p_mac_address);
    IF v_device_id IS NOT NULL THEN
        RETURN v_device_id;
    END IF;

    SELECT id
      INTO v_group_id
      FROM intellinode.device_groups
     WHERE tenant_id = p_tenant_id
       AND is_default = TRUE
     LIMIT 1;

    INSERT INTO intellinode.devices (
        tenant_id, mac_address, host_name, os, os_version, agent_version,
        group_id, is_registered, enrollment_state
    ) VALUES (
        p_tenant_id, TRIM(p_mac_address), COALESCE(p_host_name, ''), p_os,
        COALESCE(p_os_version, ''), COALESCE(p_agent_version, ''),
        v_group_id, FALSE, 'PendingInventory'
    )
    RETURNING id INTO v_device_id;

    INSERT INTO intellinode.discover_lookup (mac_address, tenant_id, status, discovered_utc)
    VALUES (TRIM(p_mac_address), p_tenant_id, 'Pending', NOW())
    ON CONFLICT (tenant_id, mac_address) DO NOTHING;

    RETURN v_device_id;
END;
$$;

COMMENT ON FUNCTION intellinode.fn_register_device(UUID, VARCHAR, VARCHAR, VARCHAR, VARCHAR, VARCHAR) IS
    'Creates device row, discover_lookup entry, and assigns default group.';

-- Supporting: queue a device task (admin API)
CREATE OR REPLACE FUNCTION intellinode.fn_queue_device_task(
    p_device_id          UUID,
    p_module_name        VARCHAR,
    p_function_name      VARCHAR,
    p_function_parameter VARCHAR DEFAULT '',
    p_legacy_task_id     INT DEFAULT 0,
    p_extra_data         VARCHAR DEFAULT ''
)
RETURNS UUID
LANGUAGE plpgsql
SET search_path = intellinode, public
AS $$
DECLARE
    v_task_id UUID;
BEGIN
    INSERT INTO intellinode.device_tasks (
        device_id, legacy_task_id, module_name, function_name,
        function_parameter, extra_data, status
    ) VALUES (
        p_device_id, COALESCE(p_legacy_task_id, 0),
        COALESCE(p_module_name, ''), COALESCE(p_function_name, ''),
        COALESCE(p_function_parameter, ''), COALESCE(p_extra_data, ''),
        0
    )
    RETURNING id INTO v_task_id;

    RETURN v_task_id;
END;
$$;

COMMENT ON FUNCTION intellinode.fn_queue_device_task(UUID, VARCHAR, VARCHAR, VARCHAR, INT, VARCHAR) IS
    'Inserts a pending device task for agent pickup.';

-- Supporting: upsert full inventory after SDFT
CREATE OR REPLACE FUNCTION intellinode.fn_upsert_device_inventory(
    p_device_id UUID,
    p_hardware  JSONB DEFAULT NULL,
    p_network   JSONB DEFAULT NULL,
    p_os_info   JSONB DEFAULT NULL,
    p_security  JSONB DEFAULT NULL
)
RETURNS VOID
LANGUAGE plpgsql
SET search_path = intellinode, public
AS $$
BEGIN
    INSERT INTO intellinode.device_inventory (
        device_id, hardware, network, os_info, security, collected_utc, version
    ) VALUES (
        p_device_id, p_hardware, p_network, p_os_info, p_security, NOW(), 1
    )
    ON CONFLICT (device_id) DO UPDATE
       SET hardware = COALESCE(EXCLUDED.hardware, intellinode.device_inventory.hardware),
           network = COALESCE(EXCLUDED.network, intellinode.device_inventory.network),
           os_info = COALESCE(EXCLUDED.os_info, intellinode.device_inventory.os_info),
           security = COALESCE(EXCLUDED.security, intellinode.device_inventory.security),
           collected_utc = NOW(),
           version = intellinode.device_inventory.version + 1;

    UPDATE intellinode.devices
       SET enrollment_state = 'Active',
           is_registered = TRUE,
           updated_utc = NOW()
     WHERE id = p_device_id;

    UPDATE intellinode.discover_lookup
       SET status = 'Approved',
           updated_utc = NOW()
     WHERE mac_address = (SELECT mac_address FROM intellinode.devices WHERE id = p_device_id);
END;
$$;

COMMENT ON FUNCTION intellinode.fn_upsert_device_inventory(UUID, JSONB, JSONB, JSONB, JSONB) IS
    'Stores full agent inventory and activates enrollment after SDFT upload.';

-- Supporting: validate agent refresh token
CREATE OR REPLACE FUNCTION intellinode.fn_validate_agent_token(
    p_device_id  UUID,
    p_token_hash TEXT
)
RETURNS BOOLEAN
LANGUAGE plpgsql
STABLE
SET search_path = intellinode, public
AS $$
BEGIN
    RETURN EXISTS (
        SELECT 1
          FROM intellinode.agent_refresh_tokens
         WHERE device_id = p_device_id
           AND token_hash = p_token_hash
           AND revoked_utc IS NULL
           AND expires_utc > NOW()
    );
END;
$$;

COMMENT ON FUNCTION intellinode.fn_validate_agent_token(UUID, TEXT) IS
    'Validates an agent refresh token hash for the given device.';

-- ============================================================
-- Step 10: Views
-- ============================================================
CREATE OR REPLACE VIEW intellinode.vw_device_summary AS
SELECT
    d.tenant_id,
    COUNT(*) AS total_devices,
    COUNT(*) FILTER (WHERE d.is_online) AS online_devices,
    COUNT(*) FILTER (WHERE NOT d.is_online) AS offline_devices,
    COUNT(*) FILTER (WHERE d.enrollment_state = 'PendingInventory') AS pending_inventory,
    COUNT(*) FILTER (WHERE d.enrollment_state = 'Active') AS active_devices
FROM intellinode.devices d
GROUP BY d.tenant_id;

CREATE OR REPLACE VIEW intellinode.vw_recent_heartbeats AS
SELECT
    d.id,
    d.tenant_id,
    d.mac_address,
    d.host_name,
    d.ip_address,
    d.client_status,
    d.is_online,
    d.last_heartbeat_utc,
    d.enrollment_state,
    ds.logged_in_user,
    ds.uptime_minutes
FROM intellinode.devices d
LEFT JOIN intellinode.device_status ds ON ds.device_id = d.id
WHERE d.last_heartbeat_utc >= NOW() - INTERVAL '24 hours';

CREATE OR REPLACE VIEW intellinode.vw_pending_tasks AS
SELECT
    dt.id,
    dt.device_id,
    dt.legacy_task_id,
    dt.module_name,
    dt.function_name,
    dt.function_parameter,
    dt.extra_data,
    dt.status,
    dt.created_utc,
    d.mac_address,
    d.host_name,
    d.tenant_id
FROM intellinode.device_tasks dt
JOIN intellinode.devices d ON d.id = dt.device_id
WHERE dt.status = 0;

-- ============================================================
-- Step 11: Seed data
-- ============================================================
INSERT INTO intellinode.tenants (id, name, host_name)
VALUES ('00000000-0000-0000-0000-000000000001', 'Default', 'localhost')
ON CONFLICT (id) DO NOTHING;

INSERT INTO intellinode.device_groups (id, tenant_id, parent_group_id, name, sort_order, is_default)
VALUES (
    '00000000-0000-0000-0000-000000000002',
    '00000000-0000-0000-0000-000000000001',
    NULL,
    'Root',
    0,
    TRUE
)
ON CONFLICT (id) DO NOTHING;

INSERT INTO intellinode.admin_users (user_name, password_hash, display_name)
VALUES (
    'admin',
    '$2a$11$1ZC1GmTBzpmt17xXGiaplOqnKVsPfEeX6FYED7WNBGBse6vd4MXke',
    'System Administrator'
)
ON CONFLICT (user_name) DO NOTHING;

-- ============================================================
-- Step 12: Grants for app role
-- ============================================================
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'intellinode_app') THEN
        CREATE ROLE intellinode_app WITH LOGIN PASSWORD 'change_me';
    END IF;
END $$;

GRANT USAGE ON SCHEMA intellinode TO intellinode_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA intellinode TO intellinode_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA intellinode TO intellinode_app;
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA intellinode TO intellinode_app;

ALTER DEFAULT PRIVILEGES IN SCHEMA intellinode
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO intellinode_app;

ALTER DEFAULT PRIVILEGES IN SCHEMA intellinode
    GRANT EXECUTE ON FUNCTIONS TO intellinode_app;

-- ============================================================
-- Done
-- ============================================================
SELECT 'Intellinode database setup complete.' AS status;
