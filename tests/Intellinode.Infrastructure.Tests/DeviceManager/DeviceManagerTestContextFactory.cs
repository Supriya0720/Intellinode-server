using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Intellinode.Infrastructure.Tests.DeviceManager;

internal static class DeviceManagerTestContextFactory
{
    public static IntellinodeDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<IntellinodeDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new IntellinodeDbContext(options);
    }

    public static Mock<IExceptionLogWriter> CreateExceptionLogWriterMock() => new();

    public static DeviceManagerTestData SeedStandardHierarchy(IntellinodeDbContext context)
    {
        var tenantId = TenantDefaults.DefaultTenantId;
        context.Tenants.Add(new Tenant { Id = tenantId, Name = "Default Tenant" });

        var headOfficeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var devTeamId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var qaTeamId = Guid.Parse("10000000-0000-0000-0000-000000000003");
        var mumbaiId = Guid.Parse("10000000-0000-0000-0000-000000000004");

        context.DeviceGroups.AddRange(
            new DeviceGroup { Id = headOfficeId, TenantId = tenantId, Name = "Head Office", SortOrder = 1 },
            new DeviceGroup { Id = devTeamId, TenantId = tenantId, Name = "Development Team", ParentGroupId = headOfficeId, SortOrder = 1 },
            new DeviceGroup { Id = qaTeamId, TenantId = tenantId, Name = "QA Team", ParentGroupId = headOfficeId, SortOrder = 2 },
            new DeviceGroup { Id = mumbaiId, TenantId = tenantId, Name = "Mumbai Branch", SortOrder = 2 });

        var headDeviceId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var devDeviceId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var unassignedDeviceId = Guid.Parse("20000000-0000-0000-0000-000000000003");
        var offlineDeviceId = Guid.Parse("20000000-0000-0000-0000-000000000004");

        context.Devices.AddRange(
            CreateDevice(headDeviceId, tenantId, headOfficeId, "head-direct", "AA:BB:CC:01", isOnline: true),
            CreateDevice(devDeviceId, tenantId, devTeamId, "dev-laptop", "AA:BB:CC:02", isOnline: true),
            CreateDevice(unassignedDeviceId, tenantId, null, "unassigned-pc", "AA:BB:CC:03", isOnline: false),
            CreateDevice(offlineDeviceId, tenantId, qaTeamId, "qa-offline", "AA:BB:CC:04", isOnline: false, enrollmentState: EnrollmentState.Disabled));

        context.SaveChanges();

        return new DeviceManagerTestData(
            TenantId: tenantId,
            HeadOfficeId: headOfficeId,
            DevTeamId: devTeamId,
            QaTeamId: qaTeamId,
            MumbaiId: mumbaiId,
            HeadDeviceId: headDeviceId,
            DevDeviceId: devDeviceId,
            UnassignedDeviceId: unassignedDeviceId,
            OfflineDeviceId: offlineDeviceId);
    }

    private static Device CreateDevice(
        Guid id,
        Guid tenantId,
        Guid? groupId,
        string hostName,
        string macAddress,
        bool isOnline,
        EnrollmentState enrollmentState = EnrollmentState.Active)
    {
        return new Device
        {
            Id = id,
            TenantId = tenantId,
            GroupId = groupId,
            HostName = hostName,
            MacAddress = macAddress,
            IsOnline = isOnline,
            ClientStatus = isOnline ? ClientPowerStatus.On : ClientPowerStatus.Off,
            EnrollmentState = enrollmentState,
            LastHeartbeatUtc = DateTime.UtcNow
        };
    }
}

internal sealed record DeviceManagerTestData(
    Guid TenantId,
    Guid HeadOfficeId,
    Guid DevTeamId,
    Guid QaTeamId,
    Guid MumbaiId,
    Guid HeadDeviceId,
    Guid DevDeviceId,
    Guid UnassignedDeviceId,
    Guid OfflineDeviceId);
