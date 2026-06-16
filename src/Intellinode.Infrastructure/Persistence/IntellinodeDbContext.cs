using Intellinode.Application.Interfaces;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Intellinode.Infrastructure.Persistence;

public sealed class IntellinodeDbContext : DbContext, IIntellinodeDbContext
{
    public const string SchemaName = "intellinode";

    public IntellinodeDbContext(DbContextOptions<IntellinodeDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceGroup> DeviceGroups => Set<DeviceGroup>();
    public DbSet<HeartbeatBindingChange> HeartbeatBindingChanges => Set<HeartbeatBindingChange>();
    public DbSet<DeviceTask> DeviceTasks => Set<DeviceTask>();
    public DbSet<AgentRefreshToken> AgentRefreshTokens => Set<AgentRefreshToken>();
    public DbSet<DeviceInventory> DeviceInventories => Set<DeviceInventory>();
    public DbSet<AgentEnrollmentToken> AgentEnrollmentTokens => Set<AgentEnrollmentToken>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<TenantAgentDefaults> TenantAgentDefaults => Set<TenantAgentDefaults>();
    public DbSet<DeviceRemoteSettings> DeviceRemoteSettings => Set<DeviceRemoteSettings>();
    public DbSet<DeviceKeyboardSettings> DeviceKeyboardSettings => Set<DeviceKeyboardSettings>();
    public DbSet<DeviceMouseSettings> DeviceMouseSettings => Set<DeviceMouseSettings>();
    public DbSet<DeviceDisplaySettings> DeviceDisplaySettings => Set<DeviceDisplaySettings>();
    public DbSet<DeviceWindows8021xSettings> DeviceWindows8021xSettings => Set<DeviceWindows8021xSettings>();
    public DbSet<DeviceWindows8021xSettingsSnapshot> DeviceWindows8021xSettingsSnapshots => Set<DeviceWindows8021xSettingsSnapshot>();
    public DbSet<DeviceWindowsComputerNameSettings> DeviceWindowsComputerNameSettings => Set<DeviceWindowsComputerNameSettings>();
    public DbSet<DeviceWindowsDateTimeSettings> DeviceWindowsDateTimeSettings => Set<DeviceWindowsDateTimeSettings>();
    public DbSet<DeviceWindowsRegionLocationSettings> DeviceWindowsRegionLocationSettings =>
        Set<DeviceWindowsRegionLocationSettings>();
    public DbSet<DeviceWindowsRegionalFormatSettings> DeviceWindowsRegionalFormatSettings =>
        Set<DeviceWindowsRegionalFormatSettings>();
    public DbSet<DeviceWindowsEthernetSettings> DeviceWindowsEthernetSettings => Set<DeviceWindowsEthernetSettings>();
    public DbSet<DeviceWindowsWirelessSetupSettings> DeviceWindowsWirelessSetupSettings => Set<DeviceWindowsWirelessSetupSettings>();
    public DbSet<DeviceWindowsWirelessProfileSettings> DeviceWindowsWirelessProfileSettings => Set<DeviceWindowsWirelessProfileSettings>();
    public DbSet<DeviceWindowsWirelessProfileSettingsSnapshot> DeviceWindowsWirelessProfileSettingsSnapshots =>
        Set<DeviceWindowsWirelessProfileSettingsSnapshot>();
    public DbSet<DeviceAgentAdvancedSettings> DeviceAgentAdvancedSettings => Set<DeviceAgentAdvancedSettings>();
    public DbSet<GroupRemoteSettings> GroupRemoteSettings => Set<GroupRemoteSettings>();
    public DbSet<GroupAgentAdvancedSettings> GroupAgentAdvancedSettings => Set<GroupAgentAdvancedSettings>();
    public DbSet<DeviceSettingsApplyLog> DeviceSettingsApplyLogs => Set<DeviceSettingsApplyLog>();
    public DbSet<DiscoverLookup> DiscoverLookups => Set<DiscoverLookup>();
    public DbSet<RegionAndLocationMaster> RegionAndLocationMasters => Set<RegionAndLocationMaster>();
    public DbSet<WindowsTimeZoneMaster> WindowsTimeZoneMasters => Set<WindowsTimeZoneMaster>();
    public DbSet<WindowsPowerPlanMaster> WindowsPowerPlanMasters => Set<WindowsPowerPlanMaster>();
    public DbSet<WindowsPowerTimeoutMaster> WindowsPowerTimeoutMasters => Set<WindowsPowerTimeoutMaster>();
    public DbSet<WindowsPowerAdvancedOptionMaster> WindowsPowerAdvancedOptionMasters => Set<WindowsPowerAdvancedOptionMaster>();
    public DbSet<DeviceWindowsPowerManagementSettings> DeviceWindowsPowerManagementSettings =>
        Set<DeviceWindowsPowerManagementSettings>();
    public DbSet<DeviceWindowsPowerManagementSettingsSnapshot> DeviceWindowsPowerManagementSettingsSnapshots =>
        Set<DeviceWindowsPowerManagementSettingsSnapshot>();
    public DbSet<DeviceWindowsScreenSaverSettings> DeviceWindowsScreenSaverSettings =>
        Set<DeviceWindowsScreenSaverSettings>();
    public DbSet<DeviceWindowsScreenSaverSettingsSnapshot> DeviceWindowsScreenSaverSettingsSnapshots =>
        Set<DeviceWindowsScreenSaverSettingsSnapshot>();
    public DbSet<DeviceWindowsTaskbarSettings> DeviceWindowsTaskbarSettings =>
        Set<DeviceWindowsTaskbarSettings>();

    public DbSet<DeviceWindowsTaskbarLiveSettings> DeviceWindowsTaskbarLiveSettings =>
        Set<DeviceWindowsTaskbarLiveSettings>();
    public DbSet<DeviceWindowsWallpaperSettings> DeviceWindowsWallpaperSettings =>
        Set<DeviceWindowsWallpaperSettings>();
    public DbSet<DeviceWindowsWallpaperSettingsSnapshot> DeviceWindowsWallpaperSettingsSnapshots =>
        Set<DeviceWindowsWallpaperSettingsSnapshot>();
    public DbSet<DeviceWindowsUserInterfaceSettings> DeviceWindowsUserInterfaceSettings =>
        Set<DeviceWindowsUserInterfaceSettings>();
    public DbSet<DeviceWindowsUserInterfaceSettingsSnapshot> DeviceWindowsUserInterfaceSettingsSnapshots =>
        Set<DeviceWindowsUserInterfaceSettingsSnapshot>();
    public DbSet<DeviceWindowsApplicationCommandSettings> DeviceWindowsApplicationCommandSettings =>
        Set<DeviceWindowsApplicationCommandSettings>();
    public DbSet<AgentCommunicationLog> AgentCommunicationLogs => Set<AgentCommunicationLog>();
    public DbSet<ExceptionLog> ExceptionLogs => Set<ExceptionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        NpgsqlModelBuilderExtensions.HasPostgresEnum(
            modelBuilder,
            "enrollment_state",
            SchemaName,
            ["PendingInventory", "Active", "Unlicensed", "Disabled", "PendingApproval", "Rejected"]);
        NpgsqlModelBuilderExtensions.HasPostgresEnum(
            modelBuilder,
            "discover_lookup_status",
            SchemaName,
            ["Pending", "Approved", "Rejected"]);
        NpgsqlModelBuilderExtensions.HasPostgresEnum(
            modelBuilder,
            "heartbeat_binding_kind",
            SchemaName,
            ["IpAddress", "HostName"]);
        NpgsqlModelBuilderExtensions.HasPostgresEnum(
            modelBuilder,
            "agent_platform",
            SchemaName,
            ["Windows", "Linux"]);
        NpgsqlModelBuilderExtensions.HasPostgresEnum(
            modelBuilder,
            "communication_type",
            SchemaName,
            ["HTTP", "HTTPS", "TCP"]);
        NpgsqlModelBuilderExtensions.HasPostgresEnum(
            modelBuilder,
            "settings_kind",
            SchemaName,
            ["General", "Advanced", "Keyboard", "Mouse", "Display", "Windows8021x", "WindowsComputerName", "WindowsEthernetSetup", "WindowsWirelessSetup", "WindowsWirelessProperties", "WindowsDateTimeSetup", "WindowsRegionLocation", "WindowsRegionalFormat", "WindowsPowerManagement", "WindowsScreenSaver", "WindowsTaskbar", "WindowsUserInterface"]);
        NpgsqlModelBuilderExtensions.HasPostgresEnum(
            modelBuilder,
            "settings_apply_status",
            SchemaName,
            ["Pending", "Delivered", "Applied", "Failed"]);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.HostName).HasMaxLength(255);
        });

        modelBuilder.Entity<DeviceGroup>(entity =>
        {
            entity.ToTable("device_groups");
            entity.HasIndex(x => new { x.TenantId, x.Name })
                .IsUnique()
                .HasFilter("parent_group_id IS NULL");
            entity.HasIndex(x => new { x.TenantId, x.ParentGroupId, x.Name })
                .IsUnique()
                .HasFilter("parent_group_id IS NOT NULL");
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.SortOrder).HasDefaultValue(0);
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.DeviceGroups)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ParentGroup)
                .WithMany(x => x.ChildGroups)
                .HasForeignKey(x => x.ParentGroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("devices");
            entity.HasIndex(x => new { x.TenantId, x.MacAddress }).IsUnique();
            entity.Property(x => x.MacAddress).HasMaxLength(300);
            entity.Property(x => x.HostName).HasMaxLength(255);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.ClientStatus).HasMaxLength(32);
            entity.Property(x => x.CommunicationType).HasMaxLength(32);
            entity.Property(x => x.AgentUpTime).HasMaxLength(64);
            entity.Property(x => x.Duration).HasMaxLength(64);
            entity.Property(x => x.Os).HasMaxLength(64);
            entity.Property(x => x.OsVersion).HasMaxLength(64);
            entity.Property(x => x.AgentVersion).HasMaxLength(64);
            entity.Property(x => x.EnrollmentState).HasColumnType("intellinode.enrollment_state");
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.Devices)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Group)
                .WithMany(x => x.Devices)
                .HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DeviceInventory>(entity =>
        {
            entity.ToTable("device_inventory");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.HardwareJson).HasColumnName("hardware").HasColumnType("jsonb");
            entity.Property(x => x.NetworkJson).HasColumnName("network").HasColumnType("jsonb");
            entity.Property(x => x.OsInfoJson).HasColumnName("os_info").HasColumnType("jsonb");
            entity.Property(x => x.SecurityJson).HasColumnName("security").HasColumnType("jsonb");
            entity.HasOne(x => x.Device)
                .WithOne(x => x.Inventory)
                .HasForeignKey<DeviceInventory>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentEnrollmentToken>(entity =>
        {
            entity.ToTable("agent_enrollment_tokens");
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.Property(x => x.MacAddress).HasMaxLength(300);
            entity.Property(x => x.Platform).HasColumnType("intellinode.agent_platform");
            entity.HasOne(x => x.Device)
                .WithMany()
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<AdminUser>()
                .WithMany()
                .HasForeignKey(x => x.CreatedByAdminId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<HeartbeatBindingChange>(entity =>
        {
            entity.ToTable("heartbeat_binding_changes");
            entity.Property(x => x.Status).HasMaxLength(32);
            entity.Property(x => x.ChangedValue).HasMaxLength(512);
            entity.Property(x => x.Kind).HasColumnType("intellinode.heartbeat_binding_kind");
        });

        modelBuilder.Entity<DeviceTask>(entity =>
        {
            entity.ToTable("device_tasks");
            entity.Property(x => x.ModuleName).HasMaxLength(128);
            entity.Property(x => x.FunctionName).HasMaxLength(128);
            entity.Property(x => x.FunctionParameter).HasMaxLength(512);
            entity.Property(x => x.ExtraData).HasMaxLength(512);
        });

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.ToTable("admin_users");
            entity.HasIndex(x => x.UserName).IsUnique();
            entity.Property(x => x.UserName).HasMaxLength(100);
            entity.Property(x => x.DisplayName).HasMaxLength(200);
        });

        modelBuilder.Entity<AgentRefreshToken>(entity =>
        {
            entity.ToTable("agent_refresh_tokens");
            entity.HasIndex(x => x.TokenHash);
        });

        modelBuilder.Entity<TenantAgentDefaults>(entity =>
        {
            entity.ToTable("tenant_agent_defaults");
            entity.HasKey(x => x.TenantId);
            entity.Property(x => x.ServerBaseUrl).HasMaxLength(512);
            entity.Property(x => x.ApiBaseUrl).HasMaxLength(512);
            entity.Property(x => x.DefaultPollIntervalSeconds).HasDefaultValue(300);
            entity.Property(x => x.DefaultCommunicationType)
                .HasColumnType("intellinode.communication_type")
                .HasDefaultValue(CommunicationType.HTTPS);
            entity.Property(x => x.MinPollIntervalHttp).HasDefaultValue(30);
            entity.HasOne(x => x.Tenant)
                .WithOne(x => x.AgentDefaults)
                .HasForeignKey<TenantAgentDefaults>(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceRemoteSettings>(entity =>
        {
            entity.ToTable("device_remote_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.ServerHost).HasMaxLength(255);
            entity.Property(x => x.ServerPort).HasDefaultValue(443);
            entity.Property(x => x.PollIntervalSeconds).HasDefaultValue(300);
            entity.Property(x => x.CommunicationType)
                .HasColumnType("intellinode.communication_type")
                .HasDefaultValue(CommunicationType.HTTPS);
            entity.Property(x => x.AgentEnabled).HasDefaultValue(true);
            entity.Property(x => x.DesiredGroupName).HasMaxLength(200);
            entity.Property(x => x.AgentHostName).HasMaxLength(255);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.Property(x => x.InheritFromGroup).HasDefaultValue(true);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_remote_settings_poll_interval_seconds",
                "poll_interval_seconds >= 1"));
            entity.HasOne(x => x.Device)
                .WithOne(x => x.RemoteSettings)
                .HasForeignKey<DeviceRemoteSettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceKeyboardSettings>(entity =>
        {
            entity.ToTable("device_keyboard_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.KeyboardLocale).HasMaxLength(100);
            entity.Property(x => x.LastApplyStatus).HasMaxLength(32);
            entity.Property(x => x.LastApplyMessage).HasMaxLength(500);
            entity.Property(x => x.Delay).HasDefaultValue(0);
            entity.Property(x => x.RepeatRate).HasDefaultValue(0);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_keyboard_settings_delay",
                "delay >= 0"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_keyboard_settings_repeat_rate",
                "repeat_rate >= 0"));
            entity.HasOne(x => x.Device)
                .WithOne(x => x.KeyboardSettings)
                .HasForeignKey<DeviceKeyboardSettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceMouseSettings>(entity =>
        {
            entity.ToTable("device_mouse_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.LastApplyStatus).HasMaxLength(32);
            entity.Property(x => x.LastApplyMessage).HasMaxLength(500);
            entity.Property(x => x.Swap).HasDefaultValue(false);
            entity.Property(x => x.PointerSpeed).HasDefaultValue(0);
            entity.Property(x => x.DoubleClickSpeed).HasDefaultValue(0);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_mouse_settings_pointer_speed",
                "pointer_speed >= 0"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_mouse_settings_double_click_speed",
                "double_click_speed >= 0"));
            entity.HasOne(x => x.Device)
                .WithOne(x => x.MouseSettings)
                .HasForeignKey<DeviceMouseSettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceDisplaySettings>(entity =>
        {
            entity.ToTable("device_display_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.Resolution).HasMaxLength(500);
            entity.Property(x => x.ColorDepth).HasMaxLength(200);
            entity.Property(x => x.DualDisplayOption).HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(x => x.SecondaryRotation).HasMaxLength(50).HasDefaultValue(string.Empty);
            entity.Property(x => x.LastApplyStatus).HasMaxLength(32);
            entity.Property(x => x.LastApplyMessage).HasMaxLength(500);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.HasOne(x => x.Device)
                .WithOne(x => x.DisplaySettings)
                .HasForeignKey<DeviceDisplaySettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindows8021xSettings>(entity =>
        {
            entity.ToTable("device_windows_802_1x_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.SettingsJson)
                .HasColumnName("settings_json")
                .HasColumnType("jsonb")
                .HasDefaultValue("{}");
            entity.Property(x => x.LastApplyStatus).HasMaxLength(32);
            entity.Property(x => x.LastApplyMessage).HasMaxLength(500);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_802_1x_settings_settings_version",
                "settings_version >= 0"));
            entity.HasOne(x => x.Device)
                .WithOne(x => x.Windows8021xSettings)
                .HasForeignKey<DeviceWindows8021xSettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsPowerManagementSettings>(entity =>
        {
            entity.ToTable("device_windows_power_management_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.ActivePlanName).HasMaxLength(50).HasDefaultValue("Balanced");
            entity.Property(x => x.AgentAction).HasDefaultValue(0);
            entity.Property(x => x.SettingsJson)
                .HasColumnName("settings_json")
                .HasColumnType("jsonb")
                .HasDefaultValue("{}");
            entity.Property(x => x.LastApplyStatus).HasMaxLength(32);
            entity.Property(x => x.LastApplyMessage).HasMaxLength(500);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_power_management_settings_settings_version",
                "settings_version >= 0"));
            entity.HasOne(x => x.Device)
                .WithOne(x => x.WindowsPowerManagementSettings)
                .HasForeignKey<DeviceWindowsPowerManagementSettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsPowerManagementSettingsSnapshot>(entity =>
        {
            entity.ToTable("device_windows_power_management_settings_snapshots");
            entity.HasKey(x => new { x.DeviceId, x.SettingsVersion });
            entity.Property(x => x.ActivePlanName).HasMaxLength(50);
            entity.Property(x => x.SettingsJson)
                .HasColumnName("settings_json")
                .HasColumnType("jsonb")
                .HasDefaultValue("{}");
            entity.HasOne(x => x.Device)
                .WithMany(x => x.WindowsPowerManagementSnapshots)
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsScreenSaverSettings>(entity =>
        {
            entity.ToTable("device_windows_screen_saver_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.ScreenSaverName).HasMaxLength(128);
            entity.Property(x => x.SourceType).HasMaxLength(32).HasDefaultValue("Browse");
            entity.Property(x => x.RepositoryJson)
                .HasColumnName("repository_json")
                .HasColumnType("jsonb");
            entity.Property(x => x.LastApplyStatus).HasMaxLength(32);
            entity.Property(x => x.LastApplyMessage).HasMaxLength(500);
            entity.Property(x => x.AgentAction).HasDefaultValue(0);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_screen_saver_settings_settings_version",
                "settings_version >= 0"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_screen_saver_settings_timeout_minutes",
                "timeout_minutes >= 0"));
            entity.HasOne(x => x.Device)
                .WithOne(x => x.WindowsScreenSaverSettings)
                .HasForeignKey<DeviceWindowsScreenSaverSettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsScreenSaverSettingsSnapshot>(entity =>
        {
            entity.ToTable("device_windows_screen_saver_settings_snapshots");
            entity.HasKey(x => new { x.DeviceId, x.SettingsVersion });
            entity.Property(x => x.ScreenSaverName).HasMaxLength(128);
            entity.Property(x => x.SourceType).HasMaxLength(32).HasDefaultValue("Browse");
            entity.Property(x => x.RepositoryJson)
                .HasColumnName("repository_json")
                .HasColumnType("jsonb");
            entity.Property(x => x.AgentAction).HasDefaultValue(0);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_screen_saver_settings_snapshots_timeout_minutes",
                "timeout_minutes >= 0"));
            entity.HasOne(x => x.Device)
                .WithMany(x => x.WindowsScreenSaverSnapshots)
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsTaskbarSettings>(entity =>
        {
            entity.ToTable("device_windows_taskbar_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.LockTaskbar).HasDefaultValue(true);
            entity.Property(x => x.KeepTaskbarOnTop).HasDefaultValue(true);
            entity.Property(x => x.GroupSimilarButtons).HasDefaultValue(true);
            entity.Property(x => x.LastApplyStatus).HasMaxLength(32);
            entity.Property(x => x.LastApplyMessage).HasMaxLength(500);
            entity.Property(x => x.AgentAction).HasDefaultValue(0);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_taskbar_settings_settings_version",
                "settings_version >= 0"));
            entity.HasOne(x => x.Device)
                .WithOne(x => x.WindowsTaskbarSettings)
                .HasForeignKey<DeviceWindowsTaskbarSettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsTaskbarLiveSettings>(entity =>
        {
            entity.ToTable("device_windows_taskbar_live_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.LockTaskbar).HasDefaultValue(true);
            entity.Property(x => x.KeepTaskbarOnTop).HasDefaultValue(true);
            entity.Property(x => x.GroupSimilarButtons).HasDefaultValue(true);
            entity.Property(x => x.ReportVersion).HasDefaultValue(1L);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_taskbar_live_settings_report_version",
                "report_version >= 1"));
            entity.HasOne(x => x.Device)
                .WithOne(x => x.WindowsTaskbarLiveSettings)
                .HasForeignKey<DeviceWindowsTaskbarLiveSettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsWallpaperSettings>(entity =>
        {
            entity.ToTable("device_windows_wallpaper_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.SourceType).HasMaxLength(32).HasDefaultValue("Browse");
            entity.Property(x => x.PicturePath).HasMaxLength(512);
            entity.Property(x => x.PictureName).HasMaxLength(256);
            entity.Property(x => x.PicturePosition).HasMaxLength(32);
            entity.Property(x => x.RepositoryJson)
                .HasColumnName("repository_json")
                .HasColumnType("jsonb");
            entity.Property(x => x.LastApplyStatus).HasMaxLength(32);
            entity.Property(x => x.LastApplyMessage).HasMaxLength(500);
            entity.Property(x => x.AgentAction).HasDefaultValue(0);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_wallpaper_settings_settings_version",
                "settings_version >= 0"));
            entity.HasOne(x => x.Device)
                .WithOne(x => x.WindowsWallpaperSettings)
                .HasForeignKey<DeviceWindowsWallpaperSettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsWallpaperSettingsSnapshot>(entity =>
        {
            entity.ToTable("device_windows_wallpaper_settings_snapshots");
            entity.HasKey(x => new { x.DeviceId, x.SettingsVersion });
            entity.Property(x => x.SourceType).HasMaxLength(32).HasDefaultValue("Browse");
            entity.Property(x => x.PicturePath).HasMaxLength(512);
            entity.Property(x => x.PictureName).HasMaxLength(256);
            entity.Property(x => x.PicturePosition).HasMaxLength(32);
            entity.Property(x => x.RepositoryJson)
                .HasColumnName("repository_json")
                .HasColumnType("jsonb");
            entity.Property(x => x.AgentAction).HasDefaultValue(0);
            entity.HasOne(x => x.Device)
                .WithMany(x => x.WindowsWallpaperSnapshots)
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsUserInterfaceSettings>(entity =>
        {
            entity.ToTable("device_windows_user_interface_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.UserName).HasMaxLength(256);
            entity.Property(x => x.PasswordCipher).HasMaxLength(1024);
            entity.Property(x => x.LastApplyStatus).HasMaxLength(32);
            entity.Property(x => x.LastApplyMessage).HasMaxLength(500);
            entity.Property(x => x.AgentAction).HasDefaultValue(0);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_user_interface_settings_settings_version",
                "settings_version >= 0"));
            entity.HasOne(x => x.Device)
                .WithOne(x => x.WindowsUserInterfaceSettings)
                .HasForeignKey<DeviceWindowsUserInterfaceSettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsUserInterfaceSettingsSnapshot>(entity =>
        {
            entity.ToTable("device_windows_user_interface_settings_snapshots");
            entity.HasKey(x => new { x.DeviceId, x.SettingsVersion });
            entity.Property(x => x.UserName).HasMaxLength(256);
            entity.Property(x => x.PasswordCipher).HasMaxLength(1024);
            entity.Property(x => x.AgentAction).HasDefaultValue(0);
            entity.HasOne(x => x.Device)
                .WithMany(x => x.WindowsUserInterfaceSnapshots)
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsApplicationCommandSettings>(entity =>
        {
            entity.ToTable("device_windows_application_command_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.Mode).HasMaxLength(16).HasDefaultValue("Application");
            entity.Property(x => x.ApplicationPath).HasMaxLength(120);
            entity.Property(x => x.Parameters).HasMaxLength(32);
            entity.Property(x => x.AlertTitle).HasMaxLength(32);
            entity.Property(x => x.AlertMessage).HasMaxLength(87);
            entity.Property(x => x.MessageType).HasMaxLength(4);
            entity.Property(x => x.DisplayTime).HasMaxLength(4);
            entity.Property(x => x.CommandText).HasMaxLength(200);
            entity.Property(x => x.Timeout).HasMaxLength(4);
            entity.Property(x => x.LastApplyStatus).HasMaxLength(32);
            entity.Property(x => x.LastApplyMessage).HasMaxLength(500);
            entity.Property(x => x.AgentAction).HasDefaultValue(0);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_application_command_settings_settings_version",
                "settings_version >= 0"));
            entity.HasOne(x => x.Device)
                .WithOne(x => x.WindowsApplicationCommandSettings)
                .HasForeignKey<DeviceWindowsApplicationCommandSettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsComputerNameSettings>(entity =>
        {
            entity.ToTable("device_windows_computer_name_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.HostName).HasMaxLength(15);
            entity.Property(x => x.Domain).HasMaxLength(63);
            entity.Property(x => x.WorkGroup).HasMaxLength(63);
            entity.Property(x => x.OrganizationalUnit).HasMaxLength(100);
            entity.Property(x => x.UserName).HasMaxLength(50);
            entity.Property(x => x.Password).HasMaxLength(64);
            entity.Property(x => x.Prefix).HasMaxLength(10);
            entity.Property(x => x.Postfix).HasMaxLength(10);
            entity.Property(x => x.NoOfChar).HasDefaultValue(0);
            entity.Property(x => x.IsMacOrSerial).HasDefaultValue(false);
            entity.Property(x => x.LastApplyStatus).HasMaxLength(32);
            entity.Property(x => x.LastApplyMessage).HasMaxLength(500);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_computer_name_settings_settings_version",
                "settings_version >= 0"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_computer_name_settings_no_of_char",
                "no_of_char >= 0"));
            entity.HasOne(x => x.Device)
                .WithOne(x => x.WindowsComputerNameSettings)
                .HasForeignKey<DeviceWindowsComputerNameSettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsDateTimeSettings>(entity =>
        {
            entity.ToTable("device_windows_date_time_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.TimeZoneDisplay).HasMaxLength(200);
            entity.Property(x => x.WindowsTzKey).HasMaxLength(50);
            entity.Property(x => x.TimeServer).HasMaxLength(255);
            entity.Property(x => x.LastApplyStatus).HasMaxLength(32);
            entity.Property(x => x.LastApplyMessage).HasMaxLength(500);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.Property(x => x.AgentAction).HasDefaultValue(0);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_date_time_settings_settings_version",
                "settings_version >= 0"));
            entity.HasOne(x => x.Device)
                .WithOne(x => x.WindowsDateTimeSettings)
                .HasForeignKey<DeviceWindowsDateTimeSettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsRegionLocationSettings>(entity =>
        {
            entity.ToTable("device_windows_region_location_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.LocationName).HasMaxLength(200);
            entity.Property(x => x.Bcp47Code).HasMaxLength(20);
            entity.Property(x => x.LanguageDescription).HasMaxLength(200);
            entity.Property(x => x.LastApplyStatus).HasMaxLength(32);
            entity.Property(x => x.LastApplyMessage).HasMaxLength(500);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.Property(x => x.AgentAction).HasDefaultValue(0);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_region_location_settings_settings_version",
                "settings_version >= 0"));
            entity.HasOne(x => x.Device)
                .WithOne(x => x.WindowsRegionLocationSettings)
                .HasForeignKey<DeviceWindowsRegionLocationSettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsRegionalFormatSettings>(entity =>
        {
            entity.ToTable("device_windows_regional_format_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.TimeFormat).HasMaxLength(50);
            entity.Property(x => x.TimeSeparator).HasMaxLength(5);
            entity.Property(x => x.AmSymbol).HasMaxLength(10);
            entity.Property(x => x.PmSymbol).HasMaxLength(10);
            entity.Property(x => x.ShortDateFormat).HasMaxLength(50);
            entity.Property(x => x.DateSeparator).HasMaxLength(5);
            entity.Property(x => x.LongDateFormat).HasMaxLength(100);
            entity.Property(x => x.ShortDateSample).HasMaxLength(50).HasDefaultValue(string.Empty);
            entity.Property(x => x.LongDateSample).HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(x => x.TimeSample).HasMaxLength(50);
            entity.Property(x => x.LastApplyStatus).HasMaxLength(32);
            entity.Property(x => x.LastApplyMessage).HasMaxLength(500);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.Property(x => x.AgentAction).HasDefaultValue(0);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_regional_format_settings_settings_version",
                "settings_version >= 0"));
            entity.HasOne(x => x.Device)
                .WithOne(x => x.WindowsRegionalFormatSettings)
                .HasForeignKey<DeviceWindowsRegionalFormatSettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsEthernetSettings>(entity =>
        {
            entity.ToTable("device_windows_ethernet_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.IpAddress).HasMaxLength(15);
            entity.Property(x => x.SubnetMask).HasMaxLength(15);
            entity.Property(x => x.Gateway).HasMaxLength(15);
            entity.Property(x => x.PrimaryDns).HasMaxLength(15);
            entity.Property(x => x.SecondaryDns).HasMaxLength(15);
            entity.Property(x => x.PrimaryWins).HasMaxLength(15);
            entity.Property(x => x.SecondaryWins).HasMaxLength(15);
            entity.Property(x => x.NetworkSpeed).HasMaxLength(64).HasDefaultValue("AutoSelect");
            entity.Property(x => x.LastApplyStatus).HasMaxLength(32);
            entity.Property(x => x.LastApplyMessage).HasMaxLength(500);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_ethernet_settings_settings_version",
                "settings_version >= 0"));
            entity.HasOne(x => x.Device)
                .WithOne(x => x.WindowsEthernetSetupSettings)
                .HasForeignKey<DeviceWindowsEthernetSettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsWirelessSetupSettings>(entity =>
        {
            entity.ToTable("device_windows_wireless_setup_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.IpAddress).HasMaxLength(15);
            entity.Property(x => x.SubnetMask).HasMaxLength(15);
            entity.Property(x => x.Gateway).HasMaxLength(15);
            entity.Property(x => x.PrimaryDns).HasMaxLength(15);
            entity.Property(x => x.SecondaryDns).HasMaxLength(15);
            entity.Property(x => x.PrimaryWins).HasMaxLength(15);
            entity.Property(x => x.SecondaryWins).HasMaxLength(15);
            entity.Property(x => x.LastApplyStatus).HasMaxLength(32);
            entity.Property(x => x.LastApplyMessage).HasMaxLength(500);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_wireless_setup_settings_settings_version",
                "settings_version >= 0"));
            entity.HasOne(x => x.Device)
                .WithOne(x => x.WindowsWirelessSetupSettings)
                .HasForeignKey<DeviceWindowsWirelessSetupSettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindows8021xSettingsSnapshot>(entity =>
        {
            entity.ToTable("device_windows_802_1x_settings_snapshots");
            entity.HasKey(x => new { x.DeviceId, x.SettingsVersion });
            entity.Property(x => x.SettingsJson)
                .HasColumnName("settings_json")
                .HasColumnType("jsonb")
                .HasDefaultValue("{}");
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_802_1x_settings_snapshots_settings_version",
                "settings_version >= 1"));
            entity.HasIndex(x => new { x.DeviceId, x.SettingsVersion })
                .IsUnique()
                .HasDatabaseName("ix_device_windows_802_1x_settings_snapshots_device_version");
            entity.HasOne(x => x.Device)
                .WithMany(x => x.Windows8021xSnapshots)
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsWirelessProfileSettings>(entity =>
        {
            entity.ToTable("device_windows_wireless_profile_settings");
            entity.HasKey(x => x.ProfileKey);
            entity.Property(x => x.ProfileKey)
                .HasColumnName("profile_key")
                .ValueGeneratedOnAdd();
            NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(entity.Property(x => x.ProfileKey));
            entity.Property(x => x.Ssid).HasMaxLength(128);
            entity.Property(x => x.SettingsJson)
                .HasColumnName("settings_json")
                .HasColumnType("jsonb")
                .HasDefaultValue("{}");
            entity.Property(x => x.LastApplyStatus).HasMaxLength(32);
            entity.Property(x => x.LastApplyMessage).HasMaxLength(500);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_wireless_profile_settings_settings_version",
                "settings_version >= 0"));
            entity.HasIndex(x => new { x.DeviceId, x.Ssid })
                .IsUnique()
                .HasDatabaseName("ix_device_windows_wireless_profile_settings_device_ssid");
            entity.HasOne(x => x.Device)
                .WithMany(x => x.WindowsWirelessProfiles)
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceWindowsWirelessProfileSettingsSnapshot>(entity =>
        {
            entity.ToTable("device_windows_wireless_profile_settings_snapshots");
            entity.HasKey(x => new { x.DeviceId, x.ProfileKey, x.SettingsVersion });
            entity.Property(x => x.SettingsJson)
                .HasColumnName("settings_json")
                .HasColumnType("jsonb")
                .HasDefaultValue("{}");
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_windows_wireless_profile_settings_snapshots_settings_version",
                "settings_version >= 1"));
            entity.HasIndex(x => new { x.DeviceId, x.ProfileKey, x.SettingsVersion })
                .IsUnique()
                .HasDatabaseName("ix_device_windows_wireless_profile_settings_snapshots_device_profile_version");
            entity.HasOne(x => x.Device)
                .WithMany(x => x.WindowsWirelessProfileSnapshots)
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Profile)
                .WithMany(x => x.Snapshots)
                .HasForeignKey(x => x.ProfileKey)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceAgentAdvancedSettings>(entity =>
        {
            entity.ToTable("device_agent_advanced_settings");
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.DebugLevel).HasDefaultValue(0);
            entity.Property(x => x.HeartbeatIntervalSeconds).HasDefaultValue(300);
            entity.Property(x => x.ApplicationIntervalSeconds).HasDefaultValue(60);
            entity.Property(x => x.UsbLogsEnabled).HasDefaultValue(false);
            entity.Property(x => x.ApplicationLogsEnabled).HasDefaultValue(false);
            entity.Property(x => x.BootLogsEnabled).HasDefaultValue(false);
            entity.Property(x => x.ScreensaverLogsEnabled).HasDefaultValue(false);
            entity.Property(x => x.YumMonitorEnabled).HasDefaultValue(false);
            entity.Property(x => x.SignalrMonitoringEnabled).HasDefaultValue(false);
            entity.Property(x => x.ConnectionType)
                .HasColumnType("intellinode.communication_type")
                .HasDefaultValue(CommunicationType.HTTPS);
            entity.Property(x => x.DhcpPollIntervalSeconds).HasDefaultValue(300);
            entity.Property(x => x.AlwaysApply).HasDefaultValue(false);
            entity.Property(x => x.ApplyOnNextReboot).HasDefaultValue(false);
            entity.Property(x => x.InheritFromGroup).HasDefaultValue(true);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.Property(x => x.PendingApply).HasDefaultValue(false);
            entity.Property(x => x.ExtraJson).HasColumnType("jsonb");
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_agent_advanced_settings_heartbeat_interval",
                "heartbeat_interval_seconds >= 1"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_agent_advanced_settings_application_interval",
                "application_interval_seconds >= 1"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_device_agent_advanced_settings_dhcp_poll_interval",
                "dhcp_poll_interval_seconds >= 1"));
            entity.HasOne(x => x.Device)
                .WithOne(x => x.AgentAdvancedSettings)
                .HasForeignKey<DeviceAgentAdvancedSettings>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GroupRemoteSettings>(entity =>
        {
            entity.ToTable("group_remote_settings");
            entity.HasKey(x => x.GroupId);
            entity.Property(x => x.ServerHost).HasMaxLength(255).HasDefaultValue(string.Empty);
            entity.Property(x => x.ServerPort).HasDefaultValue(443);
            entity.Property(x => x.PollIntervalSeconds).HasDefaultValue(300);
            entity.Property(x => x.CommunicationType)
                .HasColumnType("intellinode.communication_type")
                .HasDefaultValue(CommunicationType.HTTPS);
            entity.Property(x => x.AgentEnabled).HasDefaultValue(true);
            entity.Property(x => x.DesiredGroupName).HasMaxLength(200);
            entity.Property(x => x.AgentHostName).HasMaxLength(255);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_group_remote_settings_poll_interval_seconds",
                "poll_interval_seconds >= 1"));
            entity.HasOne(x => x.Group)
                .WithOne(x => x.RemoteSettings)
                .HasForeignKey<GroupRemoteSettings>(x => x.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GroupAgentAdvancedSettings>(entity =>
        {
            entity.ToTable("group_agent_advanced_settings");
            entity.HasKey(x => x.GroupId);
            entity.Property(x => x.DebugLevel).HasDefaultValue(0);
            entity.Property(x => x.HeartbeatIntervalSeconds).HasDefaultValue(300);
            entity.Property(x => x.ApplicationIntervalSeconds).HasDefaultValue(60);
            entity.Property(x => x.UsbLogsEnabled).HasDefaultValue(false);
            entity.Property(x => x.ApplicationLogsEnabled).HasDefaultValue(false);
            entity.Property(x => x.BootLogsEnabled).HasDefaultValue(false);
            entity.Property(x => x.ScreensaverLogsEnabled).HasDefaultValue(false);
            entity.Property(x => x.YumMonitorEnabled).HasDefaultValue(false);
            entity.Property(x => x.SignalrMonitoringEnabled).HasDefaultValue(false);
            entity.Property(x => x.ConnectionType)
                .HasColumnType("intellinode.communication_type")
                .HasDefaultValue(CommunicationType.HTTPS);
            entity.Property(x => x.DhcpPollIntervalSeconds).HasDefaultValue(300);
            entity.Property(x => x.AlwaysApply).HasDefaultValue(false);
            entity.Property(x => x.ApplyOnNextReboot).HasDefaultValue(false);
            entity.Property(x => x.SettingsVersion).HasDefaultValue(1L);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_group_agent_advanced_settings_heartbeat_interval",
                "heartbeat_interval_seconds >= 1"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_group_agent_advanced_settings_application_interval",
                "application_interval_seconds >= 1"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_group_agent_advanced_settings_dhcp_poll_interval",
                "dhcp_poll_interval_seconds >= 1"));
            entity.HasOne(x => x.Group)
                .WithOne(x => x.AgentAdvancedSettings)
                .HasForeignKey<GroupAgentAdvancedSettings>(x => x.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceSettingsApplyLog>(entity =>
        {
            entity.ToTable("device_settings_apply_log");
            entity.Property(x => x.ApplyMode).HasMaxLength(20);
            entity.Property(x => x.Message).HasMaxLength(500);
            entity.Property(x => x.SettingsKind).HasColumnType("intellinode.settings_kind");
            entity.Property(x => x.Status).HasColumnType("intellinode.settings_apply_status");
            entity.HasIndex(x => new { x.DeviceId, x.CreatedUtc })
                .IsDescending(false, true);
            entity.HasIndex(x => x.TaskId);
            entity.HasOne(x => x.Device)
                .WithMany()
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Task)
                .WithMany()
                .HasForeignKey(x => x.TaskId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DiscoverLookup>(entity =>
        {
            entity.ToTable("discover_lookup");
            entity.HasIndex(x => new { x.TenantId, x.MacAddress }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status, x.DiscoveredUtc });
            entity.Property(x => x.MacAddress).HasMaxLength(300);
            entity.Property(x => x.HostName).HasMaxLength(255);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.Domain).HasMaxLength(255);
            entity.Property(x => x.OsName).HasMaxLength(64);
            entity.Property(x => x.OsVersion).HasMaxLength(64);
            entity.Property(x => x.AgentVersion).HasMaxLength(64);
            entity.Property(x => x.DiscoveryType).HasMaxLength(64).HasDefaultValue("AgentSelfDiscovery");
            entity.Property(x => x.Status)
                .HasColumnType("intellinode.discover_lookup_status")
                .HasDefaultValue(DiscoverLookupStatus.Pending);
            entity.Property(x => x.RejectionReason).HasMaxLength(500);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Device)
                .WithMany()
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ApprovedByAdmin)
                .WithMany()
                .HasForeignKey(x => x.ApprovedByAdminId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.RejectedByAdmin)
                .WithMany()
                .HasForeignKey(x => x.RejectedByAdminId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AgentCommunicationLog>(entity =>
        {
            entity.ToTable("agent_communication_logs");
            entity.Property(x => x.MacAddress).HasMaxLength(300);
            entity.Property(x => x.Direction).HasMaxLength(16);
            entity.Property(x => x.Endpoint).HasMaxLength(256);
            entity.Property(x => x.CommandCode).HasMaxLength(16);
            entity.HasIndex(x => new { x.DeviceId, x.CreatedUtc })
                .IsDescending(false, true);
            entity.HasOne(x => x.Device)
                .WithMany()
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ExceptionLog>(entity =>
        {
            entity.ToTable("exception_logs");
            entity.Property(x => x.Source).HasMaxLength(256);
            entity.Property(x => x.Message).HasColumnType("text");
            entity.Property(x => x.StackTrace).HasColumnType("text");
            entity.Property(x => x.RequestPath).HasMaxLength(512);
            entity.Property(x => x.HttpMethod).HasMaxLength(16);
            entity.HasIndex(x => x.LoggedUtc)
                .IsDescending(true);
        });

        modelBuilder.Entity<RegionAndLocationMaster>(entity =>
        {
            entity.ToTable("region_and_location_master");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Identifier)
                .HasColumnType("character(1)")
                .HasMaxLength(1);
            entity.Property(x => x.Value).HasMaxLength(200);
            entity.Property(x => x.Bcp47Code).HasMaxLength(20);
            entity.Property(x => x.SortOrder).HasDefaultValue(0);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasIndex(x => new { x.Identifier, x.IsActive });
        });

        modelBuilder.Entity<WindowsTimeZoneMaster>(entity =>
        {
            entity.ToTable("windows_time_zone_master");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .ValueGeneratedOnAdd()
                .UseIdentityByDefaultColumn();
            entity.Property(x => x.DisplayName).HasMaxLength(200);
            entity.Property(x => x.WindowsTzKey).HasMaxLength(50);
            entity.Property(x => x.SortOrder).HasDefaultValue(0);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasIndex(x => x.DisplayName).IsUnique();
            entity.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<WindowsPowerPlanMaster>(entity =>
        {
            entity.ToTable("windows_power_plan_master");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.PlanName).HasMaxLength(50);
            entity.Property(x => x.IsDefault).HasDefaultValue(false);
            entity.Property(x => x.SortOrder).HasDefaultValue(0);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasIndex(x => x.PlanName).IsUnique();
            entity.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<WindowsPowerTimeoutMaster>(entity =>
        {
            entity.ToTable("windows_power_timeout_master");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.DisplayText).HasMaxLength(100);
            entity.Property(x => x.Category)
                .HasMaxLength(32)
                .HasConversion<string>();
            entity.Property(x => x.SortOrder).HasDefaultValue(0);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasIndex(x => new { x.Category, x.IsActive });
        });

        modelBuilder.Entity<WindowsPowerAdvancedOptionMaster>(entity =>
        {
            entity.ToTable("windows_power_advanced_option_master");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.PlanName).HasMaxLength(50);
            entity.Property(x => x.OptionName).HasMaxLength(100);
            entity.Property(x => x.SettingName).HasMaxLength(100);
            entity.Property(x => x.DisplayText).HasMaxLength(100);
            entity.Property(x => x.ValueText).HasMaxLength(100);
            entity.Property(x => x.SortOrder).HasDefaultValue(0);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasIndex(x => new { x.OptionName, x.IsActive });
            entity.HasIndex(x => new { x.PlanName, x.OptionName, x.SettingName, x.IsActive });
        });
    }
}
