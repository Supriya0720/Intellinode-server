-- User interface (autologon) settings tables (PR1). Safe to re-run.

CREATE SCHEMA IF NOT EXISTS intellinode;

DO $$
BEGIN
    ALTER TYPE intellinode.settings_kind ADD VALUE IF NOT EXISTS 'WindowsUserInterface';
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $$;

CREATE TABLE IF NOT EXISTS intellinode.device_windows_user_interface_settings (
    device_id uuid NOT NULL,
    user_name character varying(256) NOT NULL,
    auto_logon boolean NOT NULL,
    password_cipher character varying(1024) NULL,
    agent_action integer NOT NULL DEFAULT 0,
    settings_version bigint NOT NULL DEFAULT 1,
    pending_apply boolean NOT NULL DEFAULT false,
    last_applied_version bigint NULL,
    last_applied_utc timestamp with time zone NULL,
    last_apply_status character varying(32) NULL,
    last_apply_message character varying(500) NULL,
    updated_by uuid NULL,
    created_utc timestamp with time zone NOT NULL,
    updated_utc timestamp with time zone NOT NULL,
    CONSTRAINT pk_device_windows_user_interface_settings PRIMARY KEY (device_id),
    CONSTRAINT ck_device_windows_user_interface_settings_settings_version CHECK (settings_version >= 0),
    CONSTRAINT fk_device_windows_user_interface_settings_devices_device_id
        FOREIGN KEY (device_id) REFERENCES intellinode.devices (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS intellinode.device_windows_user_interface_settings_snapshots (
    device_id uuid NOT NULL,
    settings_version bigint NOT NULL,
    user_name character varying(256) NOT NULL,
    auto_logon boolean NOT NULL,
    password_cipher character varying(1024) NULL,
    agent_action integer NOT NULL DEFAULT 0,
    created_utc timestamp with time zone NOT NULL,
    CONSTRAINT pk_device_windows_user_interface_settings_snapshots PRIMARY KEY (device_id, settings_version),
    CONSTRAINT fk_device_windows_user_interface_settings_snapshots_devices_de
        FOREIGN KEY (device_id) REFERENCES intellinode.devices (id) ON DELETE CASCADE
);

INSERT INTO intellinode."__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20260615160000_AddDeviceWindowsUserInterfaceSettings', '10.0.1')
ON CONFLICT (migration_id) DO NOTHING;
