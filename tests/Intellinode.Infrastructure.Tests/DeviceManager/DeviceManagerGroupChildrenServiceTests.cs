using Intellinode.Application.Contracts.Admin;
using Intellinode.Infrastructure.Services.DeviceManager;
using Microsoft.Extensions.Logging.Abstractions;

namespace Intellinode.Infrastructure.Tests.DeviceManager;

public sealed class DeviceManagerGroupChildrenServiceTests
{
    [Fact]
    public async Task GetChildGroupsAsync_ReturnsDirectChildrenWithAggregates()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = DeviceManagerTestContextFactory.CreateContext(dbName);
        var data = DeviceManagerTestContextFactory.SeedStandardHierarchy(context);

        var service = new DeviceManagerGroupChildrenService(
            context,
            DeviceManagerTestContextFactory.CreateExceptionLogWriterMock().Object,
            NullLogger<DeviceManagerGroupChildrenService>.Instance);

        var response = await service.GetChildGroupsAsync(
            data.HeadOfficeId,
            new DeviceManagerGroupChildrenQuery());

        Assert.NotNull(response);
        Assert.Equal(data.HeadOfficeId, response!.ParentGroupId);
        Assert.Equal("Head Office", response.ParentGroupName);
        Assert.Equal(0, response.ParentDepth);
        Assert.Equal(2, response.Items.Count);

        var devTeam = response.Items.Single(i => i.Id == data.DevTeamId);
        Assert.Equal(1, devTeam.DeviceCount);
        Assert.Equal(1, devTeam.Depth);
        Assert.Equal(data.HeadOfficeId, devTeam.ParentId);
        Assert.True(devTeam.HasChildren);

        var qaTeam = response.Items.Single(i => i.Id == data.QaTeamId);
        Assert.Equal(1, qaTeam.DeviceCount);
        Assert.Equal(1, qaTeam.MaintenanceCount);
    }

    [Fact]
    public async Task GetChildGroupsAsync_ReturnsNullWhenParentMissing()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = DeviceManagerTestContextFactory.CreateContext(dbName);
        DeviceManagerTestContextFactory.SeedStandardHierarchy(context);

        var service = new DeviceManagerGroupChildrenService(
            context,
            DeviceManagerTestContextFactory.CreateExceptionLogWriterMock().Object,
            NullLogger<DeviceManagerGroupChildrenService>.Instance);

        var response = await service.GetChildGroupsAsync(
            Guid.Parse("99999999-9999-9999-9999-999999999999"),
            new DeviceManagerGroupChildrenQuery());

        Assert.Null(response);
    }

    [Fact]
    public async Task GetChildGroupsAsync_OrdersBySortOrderThenName()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = DeviceManagerTestContextFactory.CreateContext(dbName);
        var data = DeviceManagerTestContextFactory.SeedStandardHierarchy(context);

        var service = new DeviceManagerGroupChildrenService(
            context,
            DeviceManagerTestContextFactory.CreateExceptionLogWriterMock().Object,
            NullLogger<DeviceManagerGroupChildrenService>.Instance);

        var response = await service.GetChildGroupsAsync(
            data.HeadOfficeId,
            new DeviceManagerGroupChildrenQuery());

        Assert.NotNull(response);
        Assert.Equal("Development Team", response!.Items[0].Name);
        Assert.Equal("QA Team", response.Items[1].Name);
    }
}
