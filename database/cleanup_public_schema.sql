-- One-time cleanup: remove obsolete EF tables from the public schema.
-- Run against database "intellinode" AFTER migrating the API to intellinode schema.
-- Safe to re-run (IF EXISTS).

DROP TABLE IF EXISTS public."DeviceInventories" CASCADE;
DROP TABLE IF EXISTS public."AgentEnrollmentTokens" CASCADE;
DROP TABLE IF EXISTS public."AgentRefreshTokens" CASCADE;
DROP TABLE IF EXISTS public."DeviceTasks" CASCADE;
DROP TABLE IF EXISTS public."HeartbeatBindingChanges" CASCADE;
DROP TABLE IF EXISTS public."Devices" CASCADE;
DROP TABLE IF EXISTS public."DeviceGroups" CASCADE;
DROP TABLE IF EXISTS public."AdminUsers" CASCADE;
DROP TABLE IF EXISTS public."__EFMigrationsHistory" CASCADE;
