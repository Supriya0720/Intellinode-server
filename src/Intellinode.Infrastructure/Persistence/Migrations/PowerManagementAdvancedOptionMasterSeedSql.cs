namespace Intellinode.Infrastructure.Persistence.Migrations;

internal static class PowerManagementAdvancedOptionMasterSeedSql
{
    internal const string AdvancedOptionSeed = """
        INSERT INTO intellinode.windows_power_advanced_option_master (id, plan_name, option_name, setting_name, display_text, value_text, sort_order, is_active) VALUES
        (1001, NULL, 'Sleep', 'Allow hybrid sleep', 'On', 'On', 1, true),
        (1002, NULL, 'Sleep', 'Allow hybrid sleep', 'Off', 'Off', 2, true),
        (1003, NULL, 'Sleep', 'Allow wake timers', 'Enable', 'Enable', 1, true),
        (1004, NULL, 'Sleep', 'Allow wake timers', 'Disable', 'Disable', 2, true),
        (1005, NULL, 'Sleep', 'Hibernate after', 'Never', '0', 1, true),
        (1006, NULL, 'Sleep', 'Hibernate after', '1 Minute', '1', 2, true),
        (1007, NULL, 'Sleep', 'Hibernate after', '10 Minutes', '10', 3, true),
        (1008, NULL, 'Sleep', 'Hibernate after', '30 Minutes', '30', 4, true),
        (1009, NULL, 'Sleep', 'Hibernate after', '300 Minutes', '300', 5, true),
        (1010, NULL, 'Require a password on wakeup', 'Require a password on wakeup', 'Yes', 'Yes', 1, true),
        (1011, NULL, 'Require a password on wakeup', 'Require a password on wakeup', 'No', 'No', 2, true),
        (1012, NULL, 'Slide show', 'Slide show', 'Available', 'Available', 1, true),
        (1013, NULL, 'Slide show', 'Slide show', 'Paused', 'Paused', 2, true),
        (1014, NULL, 'Power saving mode', 'Power saving mode', 'Maximum Performance', 'Maximum Performance', 1, true),
        (1015, NULL, 'Power saving mode', 'Power saving mode', 'Low Power Saving', 'Low Power Saving', 2, true),
        (1016, NULL, 'Power saving mode', 'Power saving mode', 'Medium Power Saving', 'Medium Power Saving', 3, true),
        (1017, NULL, 'Power saving mode', 'Power saving mode', 'Maximum Power Saving', 'Maximum Power Saving', 4, true),
        (1018, NULL, 'USB selective suspend setting', 'USB selective suspend setting', 'Enable', 'Enable', 1, true),
        (1019, NULL, 'USB selective suspend setting', 'USB selective suspend setting', 'Disable', 'Disable', 2, true),
        (1020, NULL, 'Link state power management', 'Link state power management', 'Off', 'Off', 1, true),
        (1021, NULL, 'Link state power management', 'Link state power management', 'Moderate power savings', 'Moderate power savings', 2, true),
        (1022, NULL, 'Link state power management', 'Link state power management', 'Maximum power savings', 'Maximum power savings', 3, true),
        (1023, NULL, 'Minimum processor state', 'Minimum processor state', 'Never', '0', 1, true),
        (1024, NULL, 'Minimum processor state', 'Minimum processor state', '10 Minutes', '10', 2, true),
        (1025, NULL, 'Minimum processor state', 'Minimum processor state', '300 Minutes', '300', 3, true),
        (1026, NULL, 'System cooling policy', 'System cooling policy', 'Never', '0', 1, true),
        (1027, NULL, 'System cooling policy', 'System cooling policy', '10 Minutes', '10', 2, true),
        (1028, NULL, 'System cooling policy', 'System cooling policy', '300 Minutes', '300', 3, true),
        (1029, NULL, 'Maximum processor state', 'Maximum processor state', 'Never', '0', 1, true),
        (1030, NULL, 'Maximum processor state', 'Maximum processor state', '10 Minutes', '10', 2, true),
        (1031, NULL, 'Maximum processor state', 'Maximum processor state', '300 Minutes', '300', 3, true),
        (1032, NULL, 'When sharing media', 'When sharing media', 'Allow the computer to sleep', 'Allow the computer to sleep', 1, true),
        (1033, NULL, 'When sharing media', 'When sharing media', 'Prevent idling to sleep', 'Prevent idling to sleep', 2, true),
        (1034, NULL, 'When sharing media', 'When sharing media', 'Allow the computer to enter away mode', 'Allow the computer to enter away mode', 3, true),
        (1035, NULL, 'When playing video', 'When playing video', 'Optimize video quality', 'Optimize video quality', 1, true),
        (1036, NULL, 'When playing video', 'When playing video', 'Balanced', 'Balanced', 2, true),
        (1037, NULL, 'When playing video', 'When playing video', 'Optimize power saving', 'Optimize power saving', 3, true);
        """;
}
