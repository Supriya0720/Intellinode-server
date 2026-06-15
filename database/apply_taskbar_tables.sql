-- Taskbar settings table (PR1). Safe to re-run.

CREATE SCHEMA IF NOT EXISTS intellinode;

DO $$
BEGIN
    ALTER TYPE intellinode.settings_kind ADD VALUE IF NOT EXISTS 'WindowsTaskbar';
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $$;

CREATE TABLE IF NOT EXISTS intellinode.device_windows_taskbar_settings (
    device_id uuid NOT NULL,
    lock_taskbar boolean NOT NULL DEFAULT true,
    auto_hide_taskbar boolean NOT NULL,
    keep_taskbar_on_top boolean NOT NULL DEFAULT true,
    group_similar_buttons boolean NOT NULL DEFAULT true,
    show_quick_launch boolean NOT NULL,
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
    CONSTRAINT pk_device_windows_taskbar_settings PRIMARY KEY (device_id),
    CONSTRAINT ck_device_windows_taskbar_settings_settings_version CHECK (settings_version >= 0),
    CONSTRAINT fk_device_windows_taskbar_settings_devices_device_id
        FOREIGN KEY (device_id) REFERENCES intellinode.devices (id) ON DELETE CASCADE
);

INSERT INTO intellinode."__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20260615120000_AddDeviceWindowsTaskbarSettings', '8.0.0')
ON CONFLICT (migration_id) DO NOTHING;

CREATE TABLE IF NOT EXISTS intellinode.device_windows_taskbar_live_settings (
    device_id uuid NOT NULL,
    lock_taskbar boolean NOT NULL DEFAULT true,
    auto_hide_taskbar boolean NOT NULL,
    keep_taskbar_on_top boolean NOT NULL DEFAULT true,
    group_similar_buttons boolean NOT NULL DEFAULT true,
    show_quick_launch boolean NOT NULL,
    show_clock boolean NOT NULL,
    hide_inactive_icons boolean NOT NULL,
    collected_utc timestamp with time zone NOT NULL,
    report_version bigint NOT NULL DEFAULT 1,
    created_utc timestamp with time zone NOT NULL,
    updated_utc timestamp with time zone NOT NULL,
    CONSTRAINT pk_device_windows_taskbar_live_settings PRIMARY KEY (device_id),
    CONSTRAINT ck_device_windows_taskbar_live_settings_report_version CHECK (report_version >= 1),
    CONSTRAINT fk_device_windows_taskbar_live_settings_devices_device_id
        FOREIGN KEY (device_id) REFERENCES intellinode.devices (id) ON DELETE CASCADE
);

INSERT INTO intellinode."__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20260615140000_AddDeviceWindowsTaskbarLiveSettings', '8.0.0')
ON CONFLICT (migration_id) DO NOTHING;
