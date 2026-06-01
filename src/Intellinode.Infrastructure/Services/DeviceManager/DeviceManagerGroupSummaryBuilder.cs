using Intellinode.Application.Contracts.Admin;

namespace Intellinode.Infrastructure.Services.DeviceManager;

internal static class DeviceManagerGroupSummaryBuilder
{
    public static DeviceManagerGroupSummaryDto? BuildSummary(
        DeviceManagerGroupRow group,
        Guid? parentId,
        int depth,
        IReadOnlyList<DeviceManagerGroupRow> allGroups,
        IReadOnlyDictionary<Guid, List<DeviceManagerGroupRow>> childrenByParentId,
        IReadOnlyList<DeviceManagerDeviceRow> statusFilteredDevices,
        string? searchTerm,
        bool hideEmptyWhenFiltered)
    {
        var groupById = allGroups.ToDictionary(g => g.Id);
        var subtreeIds = DeviceManagerGroupHierarchyHelper.CollectSubtreeIds(allGroups, group.Id);
        var subtreeDevices = statusFilteredDevices
            .Where(d => d.GroupId.HasValue && subtreeIds.Contains(d.GroupId.Value))
            .ToList();

        if (hideEmptyWhenFiltered && subtreeDevices.Count == 0)
        {
            return null;
        }

        if (searchTerm is not null && !IsVisibleForSearch(group, subtreeIds, groupById, subtreeDevices, searchTerm))
        {
            return null;
        }

        var aggregates = DeviceManagerAggregateCalculator.ComputeFromDevices(subtreeDevices);
        var hasDirectChildGroups = childrenByParentId.ContainsKey(group.Id);
        var hasDirectDevices = statusFilteredDevices.Any(d => d.GroupId == group.Id);
        var hasDescendantDevices = subtreeDevices.Any(d => d.GroupId != group.Id);

        return new DeviceManagerGroupSummaryDto
        {
            Id = group.Id,
            NodeType = DeviceManagerNodeType.Group,
            Name = group.Name,
            ParentId = parentId,
            Depth = depth,
            SortOrder = group.SortOrder,
            HasChildren = hasDirectChildGroups || hasDirectDevices || hasDescendantDevices,
            DeviceCount = aggregates.DeviceCount,
            OnlineCount = aggregates.OnlineCount,
            OfflineCount = aggregates.OfflineCount,
            MaintenanceCount = aggregates.MaintenanceCount
        };
    }

    public static DeviceManagerGroupSummaryDto BuildUnassignedSummary(
        IReadOnlyList<DeviceManagerDeviceRow> unassignedDevices,
        string? searchTerm)
    {
        var visibleDevices = searchTerm is null
            ? unassignedDevices
            : unassignedDevices
                .Where(d => DeviceManagerDeviceQueryHelper.DeviceMatchesSearch(d, searchTerm))
                .ToList();

        var aggregates = DeviceManagerAggregateCalculator.ComputeFromDevices(visibleDevices);

        return new DeviceManagerGroupSummaryDto
        {
            Id = DeviceManagerConstants.UnassignedNodeId,
            NodeType = DeviceManagerNodeType.Unassigned,
            Name = "Unassigned",
            ParentId = null,
            Depth = 0,
            SortOrder = int.MaxValue,
            HasChildren = visibleDevices.Count > 0,
            DeviceCount = aggregates.DeviceCount,
            OnlineCount = aggregates.OnlineCount,
            OfflineCount = aggregates.OfflineCount,
            MaintenanceCount = aggregates.MaintenanceCount
        };
    }

    private static bool IsVisibleForSearch(
        DeviceManagerGroupRow group,
        HashSet<Guid> subtreeIds,
        IReadOnlyDictionary<Guid, DeviceManagerGroupRow> groupById,
        IReadOnlyList<DeviceManagerDeviceRow> subtreeDevices,
        string searchTerm)
    {
        if (DeviceManagerDeviceQueryHelper.GroupNameMatchesSearch(group.Name, searchTerm))
        {
            return true;
        }

        foreach (var groupId in subtreeIds)
        {
            if (groupId == group.Id)
            {
                continue;
            }

            if (groupById.TryGetValue(groupId, out var descendant) &&
                DeviceManagerDeviceQueryHelper.GroupNameMatchesSearch(descendant.Name, searchTerm))
            {
                return true;
            }
        }

        return subtreeDevices.Any(d => DeviceManagerDeviceQueryHelper.DeviceMatchesSearch(d, searchTerm));
    }
}
