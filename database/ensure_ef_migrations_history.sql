-- Creates EF Core migration history in the intellinode schema.
-- Use when intellinode_full_setup.sql was applied first and EF migrations did not run.
-- Column names use snake_case because EFCore.NamingConventions is enabled.

CREATE SCHEMA IF NOT EXISTS intellinode;

CREATE TABLE IF NOT EXISTS intellinode."__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL PRIMARY KEY,
    product_version character varying(32) NOT NULL
);

INSERT INTO intellinode."__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20260523055049_InitialIntellinodeSchema', '10.0.1')
ON CONFLICT (migration_id) DO NOTHING;
