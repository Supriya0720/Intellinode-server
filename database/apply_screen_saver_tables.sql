-- Screen saver tables (PR1 + PR3). Safe to re-run.
-- Run on database intellinode if execute-now fails with:
--   relation "intellinode.device_windows_screen_saver_settings" does not exist

CREATE SCHEMA IF NOT EXISTS intellinode;

CREATE TABLE IF NOT EXISTS intellinode."__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL PRIMARY KEY,
    product_version character varying(32) NOT NULL
);

DO $$
BEGIN
    ALTER TYPE intellinode.settings_kind ADD VALUE IF NOT EXISTS 'WindowsScreenSaver';
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $$;

CREATE TABLE IF NOT EXISTS intellinode.device_windows_screen_saver_settings (
    device_id uuid NOT NULL,
    screen_saver_name character varying(128) NOT NULL,
    timeout_minutes integer NOT NULL DEFAULT 0,
    password_protected boolean NOT NULL,
    prevent_user_changes boolean NOT NULL,
    source_type character varying(32) NOT NULL DEFAULT 'Browse',
    upload boolean NOT NULL,
    agent_action integer NOT NULL DEFAULT 0,
    repository_json jsonb NULL,
    settings_version bigint NOT NULL DEFAULT 1,
    pending_apply boolean NOT NULL DEFAULT false,
    last_applied_version bigint NULL,
    last_applied_utc timestamp with time zone NULL,
    last_apply_status character varying(32) NULL,
    last_apply_message character varying(500) NULL,
    updated_by uuid NULL,
    created_utc timestamp with time zone NOT NULL,
    updated_utc timestamp with time zone NOT NULL,
    CONSTRAINT pk_device_windows_screen_saver_settings PRIMARY KEY (device_id),
    CONSTRAINT ck_device_windows_screen_saver_settings_settings_version CHECK (settings_version >= 0),
    CONSTRAINT ck_device_windows_screen_saver_settings_timeout_minutes CHECK (timeout_minutes >= 0),
    CONSTRAINT fk_device_windows_screen_saver_settings_devices_device_id
        FOREIGN KEY (device_id) REFERENCES intellinode.devices (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS intellinode.device_windows_screen_saver_settings_snapshots (
    device_id uuid NOT NULL,
    settings_version bigint NOT NULL,
    screen_saver_name character varying(128) NOT NULL,
    timeout_minutes integer NOT NULL DEFAULT 0,
    password_protected boolean NOT NULL,
    prevent_user_changes boolean NOT NULL,
    source_type character varying(32) NOT NULL DEFAULT 'Browse',
    upload boolean NOT NULL,
    agent_action integer NOT NULL DEFAULT 0,
    repository_json jsonb NULL,
    created_utc timestamp with time zone NOT NULL,
    CONSTRAINT pk_device_windows_screen_saver_settings_snapshots PRIMARY KEY (device_id, settings_version),
    CONSTRAINT ck_device_windows_screen_saver_settings_snapshots_timeout_minutes CHECK (timeout_minutes >= 0),
    CONSTRAINT fk_device_windows_screen_saver_settings_snapshots_devices_devi
        FOREIGN KEY (device_id) REFERENCES intellinode.devices (id) ON DELETE CASCADE
);

INSERT INTO intellinode."__EFMigrationsHistory" (migration_id, product_version)
VALUES
    ('20260612120000_AddDeviceWindowsScreenSaverSettings', '10.0.1'),
    ('20260612130000_AddDeviceWindowsScreenSaverSettingsSnapshots', '10.0.1')
ON CONFLICT (migration_id) DO NOTHING;
