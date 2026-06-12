namespace Intellinode.Infrastructure.Persistence.Migrations;

internal static class PowerManagementReferenceMasterSeedSql
{
    internal const string PowerPlanSeed = """
        INSERT INTO intellinode.windows_power_plan_master (id, plan_name, is_default, sort_order, is_active) VALUES
        (1, 'Balanced', true, 1, true),
        (2, 'High performance', false, 2, true),
        (3, 'Power saver', false, 3, true);
        """;

    internal const string TimeoutSeed = """
        INSERT INTO intellinode.windows_power_timeout_master (id, display_text, value_seconds, category, sort_order, is_active) VALUES
        (101, '1 Minute', 60, 'Display', 1, true),
        (102, '5 Minutes', 300, 'Display', 2, true),
        (103, '10 Minutes', 600, 'Display', 3, true),
        (104, '15 Minutes', 900, 'Display', 4, true),
        (105, '30 Minutes', 1800, 'Display', 5, true),
        (106, '1 Hour', 3600, 'Display', 6, true),
        (107, 'Never', NULL, 'Display', 7, true),
        (201, '1 Minute', 60, 'Sleep', 1, true),
        (202, '5 Minutes', 300, 'Sleep', 2, true),
        (203, '10 Minutes', 600, 'Sleep', 3, true),
        (204, '15 Minutes', 900, 'Sleep', 4, true),
        (205, '30 Minutes', 1800, 'Sleep', 5, true),
        (206, '1 Hour', 3600, 'Sleep', 6, true),
        (207, 'Never', NULL, 'Sleep', 7, true),
        (301, '1 Minute', 60, 'HardDisk', 1, true),
        (302, '5 Minutes', 300, 'HardDisk', 2, true),
        (303, '10 Minutes', 600, 'HardDisk', 3, true),
        (304, '15 Minutes', 900, 'HardDisk', 4, true),
        (305, '30 Minutes', 1800, 'HardDisk', 5, true),
        (306, '1 Hour', 3600, 'HardDisk', 6, true),
        (307, 'Never', NULL, 'HardDisk', 7, true),
        (401, '1 Minute', 60, 'SystemStandby', 1, true),
        (402, '5 Minutes', 300, 'SystemStandby', 2, true),
        (403, '10 Minutes', 600, 'SystemStandby', 3, true),
        (404, '15 Minutes', 900, 'SystemStandby', 4, true),
        (405, '30 Minutes', 1800, 'SystemStandby', 5, true),
        (406, '1 Hour', 3600, 'SystemStandby', 6, true),
        (407, 'Never', NULL, 'SystemStandby', 7, true),
        (501, 'Shut down', NULL, 'PowerButton', 1, true),
        (502, 'Do nothing', NULL, 'PowerButton', 2, true),
        (503, 'Sleep', NULL, 'PowerButton', 3, true),
        (504, 'Hibernate', NULL, 'PowerButton', 4, true),
        (505, 'Ask me what to do', NULL, 'PowerButton', 5, true),
        (506, 'Stand by', NULL, 'PowerButton', 6, true),
        (601, 'Shut down', NULL, 'SleepButton', 1, true),
        (602, 'Do nothing', NULL, 'SleepButton', 2, true),
        (603, 'Sleep', NULL, 'SleepButton', 3, true),
        (604, 'Hibernate', NULL, 'SleepButton', 4, true),
        (605, 'Ask me what to do', NULL, 'SleepButton', 5, true),
        (606, 'Stand by', NULL, 'SleepButton', 6, true);
        """;
}
