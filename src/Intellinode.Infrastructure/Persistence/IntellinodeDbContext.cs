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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        NpgsqlModelBuilderExtensions.HasPostgresEnum(
            modelBuilder,
            "enrollment_state",
            SchemaName,
            ["PendingInventory", "Active", "Unlicensed", "Disabled"]);
        NpgsqlModelBuilderExtensions.HasPostgresEnum(
            modelBuilder,
            "heartbeat_binding_kind",
            SchemaName,
            ["IpAddress", "HostName"]);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.HostName).HasMaxLength(255);
        });

        modelBuilder.Entity<DeviceGroup>(entity =>
        {
            entity.ToTable("device_groups");
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.DeviceGroups)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
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
    }
}
