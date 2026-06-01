using Intellinode.Application.Contracts.Admin;
using Intellinode.Infrastructure.Services.DeviceManager;
using Microsoft.Extensions.Logging.Abstractions;

namespace Intellinode.Infrastructure.Tests.DeviceManager;

public sealed class DeviceManagerGroupDevicesServiceTests
{
    [Fact]
    public async Task GetGroupDevicesAsync_ReturnsDirectDevicesOnlyWithPagination()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = DeviceManagerTestContextFactory.CreateContext(dbName);
        var data = DeviceManagerTestContextFactory.SeedStandardHierarchy(context);

        var service = new DeviceManagerGroupDevicesService(
            context,
            DeviceManagerTestContextFactory.CreateExceptionLogWriterMock().Object,
            NullLogger<DeviceManagerGroupDevicesService>.Instance);

        var response = await service.GetGroupDevicesAsync(
            data.HeadOfficeId,
            new DeviceManagerGroupDevicesQuery { Page = 1, PageSize = 10 });

        Assert.NotNull(response);
        Assert.Equal(1, response!.TotalCount);
        Assert.Single(response.Items);
        Assert.Equal(data.HeadDeviceId, response.Items[0].Id);
        Assert.Equal("head-direct", response.Items[0].HostName);
        Assert.Equal(data.HeadOfficeId, response.GroupId);
        Assert.Equal("Head Office", response.GroupName);
    }

    [Fact]
    public async Task GetGroupDevicesAsync_ReturnsNullWhenGroupMissing()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = DeviceManagerTestContextFactory.CreateContext(dbName);
        DeviceManagerTestContextFactory.SeedStandardHierarchy(context);

        var service = new DeviceManagerGroupDevicesService(
            context,
            DeviceManagerTestContextFactory.CreateExceptionLogWriterMock().Object,
            NullLogger<DeviceManagerGroupDevicesService>.Instance);

        var response = await service.GetGroupDevicesAsync(
            Guid.Parse("99999999-9999-9999-9999-999999999999"),
            new DeviceManagerGroupDevicesQuery());

        Assert.Null(response);
    }

    [Fact]
    public async Task GetGroupDevicesAsync_StatusFilter_AppliesAtDatabaseLevel()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = DeviceManagerTestContextFactory.CreateContext(dbName);
        var data = DeviceManagerTestContextFactory.SeedStandardHierarchy(context);

        var service = new DeviceManagerGroupDevicesService(
            context,
            DeviceManagerTestContextFactory.CreateExceptionLogWriterMock().Object,
            NullLogger<DeviceManagerGroupDevicesService>.Instance);

        var response = await service.GetGroupDevicesAsync(
            data.QaTeamId,
            new DeviceManagerGroupDevicesQuery { Status = "Maintenance" });

        Assert.NotNull(response);
        Assert.Equal(1, response!.TotalCount);
        Assert.Equal("Maintenance", response.Items[0].Status);
    }

    [Fact]
    public async Task GetGroupDevicesAsync_ClampsPageSizeToMax200()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = DeviceManagerTestContextFactory.CreateContext(dbName);
        var data = DeviceManagerTestContextFactory.SeedStandardHierarchy(context);

        var service = new DeviceManagerGroupDevicesService(
            context,
            DeviceManagerTestContextFactory.CreateExceptionLogWriterMock().Object,
            NullLogger<DeviceManagerGroupDevicesService>.Instance);

        var response = await service.GetGroupDevicesAsync(
            data.HeadOfficeId,
            new DeviceManagerGroupDevicesQuery { PageSize = 500 });

        Assert.NotNull(response);
        Assert.Equal(200, response!.PageSize);
    }

    [Fact]
    public async Task GetUnassignedDevicesAsync_ReturnsUnassignedDevicesOnly()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = DeviceManagerTestContextFactory.CreateContext(dbName);
        var data = DeviceManagerTestContextFactory.SeedStandardHierarchy(context);

        var service = new DeviceManagerGroupDevicesService(
            context,
            DeviceManagerTestContextFactory.CreateExceptionLogWriterMock().Object,
            NullLogger<DeviceManagerGroupDevicesService>.Instance);

        var response = await service.GetUnassignedDevicesAsync(new DeviceManagerGroupDevicesQuery());

        Assert.Equal(1, response.TotalCount);
        Assert.Equal(data.UnassignedDeviceId, response.Items[0].Id);
        Assert.Equal("Unassigned", response.GroupName);
        Assert.Equal(Guid.Empty, response.GroupId);
    }

    [Fact]
    public async Task GetGroupDevicesAsync_PaginatesCorrectly()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = DeviceManagerTestContextFactory.CreateContext(dbName);
        var tenantId = Domain.TenantDefaults.DefaultTenantId;
        context.Tenants.Add(new Domain.Entities.Tenant { Id = tenantId, Name = "Default Tenant" });

        var groupId = Guid.NewGuid();
        context.DeviceGroups.Add(new Domain.Entities.DeviceGroup
        {
            Id = groupId,
            TenantId = tenantId,
            Name = "Paged Group"
        });

        for (var i = 0; i < 30; i++)
        {
            context.Devices.Add(new Domain.Entities.Device
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                GroupId = groupId,
                HostName = $"device-{i:D2}",
                MacAddress = $"AA:BB:CC:{i:D2}",
                ClientStatus = Domain.Enums.ClientPowerStatus.Off
            });
        }

        await context.SaveChangesAsync();

        var service = new DeviceManagerGroupDevicesService(
            context,
            DeviceManagerTestContextFactory.CreateExceptionLogWriterMock().Object,
            NullLogger<DeviceManagerGroupDevicesService>.Instance);

        var page1 = await service.GetGroupDevicesAsync(
            groupId,
            new DeviceManagerGroupDevicesQuery { Page = 1, PageSize = 25 });

        var page2 = await service.GetGroupDevicesAsync(
            groupId,
            new DeviceManagerGroupDevicesQuery { Page = 2, PageSize = 25 });

        Assert.NotNull(page1);
        Assert.NotNull(page2);
        Assert.Equal(30, page1!.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(25, page1.Items.Count);
        Assert.Equal(5, page2!.Items.Count);
    }
}
