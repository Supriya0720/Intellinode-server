using Intellinode.Application.Contracts.Admin;
using Intellinode.Infrastructure.Services.DeviceManager;
using Microsoft.Extensions.Logging.Abstractions;

namespace Intellinode.Infrastructure.Tests.DeviceManager;

public sealed class DeviceManagerRootsServiceTests
{
    [Fact]
    public async Task GetRootsAsync_ReturnsRootGroupsWithSubtreeAggregates()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = DeviceManagerTestContextFactory.CreateContext(dbName);
        var data = DeviceManagerTestContextFactory.SeedStandardHierarchy(context);

        var service = new DeviceManagerRootsService(
            context,
            DeviceManagerTestContextFactory.CreateExceptionLogWriterMock().Object,
            NullLogger<DeviceManagerRootsService>.Instance);

        var response = await service.GetRootsAsync(new DeviceManagerRootsQuery());

        Assert.Equal(4, response.TotalDeviceCount);
        Assert.Equal(4, response.FilteredDeviceCount);
        Assert.Equal(3, response.Items.Count);

        var headOffice = response.Items.Single(i => i.Id == data.HeadOfficeId);
        Assert.Equal(DeviceManagerNodeType.Group, headOffice.NodeType);
        Assert.Equal(3, headOffice.DeviceCount);
        Assert.True(headOffice.HasChildren);
        Assert.Equal(0, headOffice.Depth);

        var mumbai = response.Items.Single(i => i.Id == data.MumbaiId);
        Assert.Equal(0, mumbai.DeviceCount);
        Assert.False(mumbai.HasChildren);

        var unassigned = response.Items.Single(i => i.NodeType == DeviceManagerNodeType.Unassigned);
        Assert.Equal(1, unassigned.DeviceCount);
        Assert.True(unassigned.HasChildren);
    }

    [Fact]
    public async Task GetRootsAsync_ExcludesUnassignedWhenDisabled()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = DeviceManagerTestContextFactory.CreateContext(dbName);
        DeviceManagerTestContextFactory.SeedStandardHierarchy(context);

        var service = new DeviceManagerRootsService(
            context,
            DeviceManagerTestContextFactory.CreateExceptionLogWriterMock().Object,
            NullLogger<DeviceManagerRootsService>.Instance);

        var response = await service.GetRootsAsync(new DeviceManagerRootsQuery { IncludeUnassigned = false });

        Assert.DoesNotContain(response.Items, i => i.NodeType == DeviceManagerNodeType.Unassigned);
        Assert.Equal(2, response.Items.Count);
    }

    [Fact]
    public async Task GetRootsAsync_StatusFilter_HidesEmptyGroupsAndAdjustsCounts()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = DeviceManagerTestContextFactory.CreateContext(dbName);
        var data = DeviceManagerTestContextFactory.SeedStandardHierarchy(context);

        var service = new DeviceManagerRootsService(
            context,
            DeviceManagerTestContextFactory.CreateExceptionLogWriterMock().Object,
            NullLogger<DeviceManagerRootsService>.Instance);

        var response = await service.GetRootsAsync(new DeviceManagerRootsQuery
        {
            Status = "Maintenance",
            IncludeUnassigned = false
        });

        Assert.Single(response.Items);
        var headOffice = response.Items[0];
        Assert.Equal(data.HeadOfficeId, headOffice.Id);
        Assert.Equal(1, headOffice.DeviceCount);
        Assert.Equal(1, headOffice.MaintenanceCount);
    }

    [Fact]
    public async Task GetRootsAsync_SearchFilter_ShowsMatchingRootOrSubtree()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = DeviceManagerTestContextFactory.CreateContext(dbName);
        var data = DeviceManagerTestContextFactory.SeedStandardHierarchy(context);

        var service = new DeviceManagerRootsService(
            context,
            DeviceManagerTestContextFactory.CreateExceptionLogWriterMock().Object,
            NullLogger<DeviceManagerRootsService>.Instance);

        var response = await service.GetRootsAsync(new DeviceManagerRootsQuery
        {
            Search = "dev-laptop",
            IncludeUnassigned = false
        });

        Assert.Single(response.Items);
        Assert.Equal(data.HeadOfficeId, response.Items[0].Id);
    }
}
