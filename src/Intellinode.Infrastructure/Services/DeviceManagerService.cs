using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services;

public sealed class DeviceManagerService : IDeviceManagerService
{
    private const string ServiceSource = nameof(DeviceManagerService);
    private const string GroupNodeType = "group";
    private const string SubgroupNodeType = "subgroup";
    private const string DeviceNodeType = "device";

    /// <summary>Synthetic root id for devices without a group (<see cref="Guid.Empty"/>).</summary>
    public static readonly Guid UnassignedNodeId = Guid.Empty;

    private readonly IntellinodeDbContext _dbContext;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<DeviceManagerService> _logger;

    public DeviceManagerService(
        IntellinodeDbContext dbContext,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<DeviceManagerService> logger)
    {
        _dbContext = dbContext;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
    }

    public async Task<DeviceTreeResponse> GetTreeAsync(
        DeviceTreeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            var tenantId = TenantDefaults.DefaultTenantId;
            var groups = await LoadGroupsAsync(tenantId, cancellationToken);
            var devices = await LoadDevicesAsync(tenantId, cancellationToken);
            return BuildTreeResponse(query, groups, devices);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await ExceptionLogHelper.SafeLogAsync(
                _exceptionLogWriter,
                _logger,
                $"{ServiceSource}.{nameof(GetTreeAsync)}",
                ex,
                cancellationToken: cancellationToken);
            throw;
        }
    }

    public async Task<DeviceManagerGroupInfoDto?> GetGroupInfoAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = TenantDefaults.DefaultTenantId;
            var group = await _dbContext.DeviceGroups
                .AsNoTracking()
                .Where(g => g.Id == groupId && g.TenantId == tenantId)
                .Select(g => new GroupDetailRow(
                    g.Id,
                    g.Name,
                    g.ParentGroupId,
                    g.IsDefault,
                    g.SortOrder,
                    g.CreatedUtc,
                    g.ParentGroup != null ? g.ParentGroup.Name : null))
                .FirstOrDefaultAsync(cancellationToken);

            if (group is null)
            {
                return null;
            }

            var groups = await LoadGroupsAsync(tenantId, cancellationToken);
            var devices = await LoadDevicesAsync(tenantId, cancellationToken);
            var groupById = groups.ToDictionary(g => g.Id);

            var descendantIds = CollectSubtreeIds(groups, groupId);
            var subtreeDevices = devices
                .Where(d => d.GroupId.HasValue && descendantIds.Contains(d.GroupId.Value))
                .ToList();

            var statusCounts = CountDeviceStatuses(subtreeDevices);
            var hierarchy = BuildGroupHierarchy(groups);
            var directChildren = hierarchy.ChildrenByParentId.TryGetValue(groupId, out var children)
                ? children
                : [];

            var childDtos = directChildren
                .Select(child =>
                {
                    var childSubtreeIds = CollectSubtreeIds(groups, child.Id);
                    var deviceCount = devices.Count(d =>
                        d.GroupId.HasValue && childSubtreeIds.Contains(d.GroupId.Value));
                    return new DeviceManagerGroupChildDto
                    {
                        Id = child.Id,
                        Name = child.Name,
                        SortOrder = child.SortOrder,
                        DeviceCount = deviceCount
                    };
                })
                .ToList();

            var remoteSettings = await _dbContext.GroupRemoteSettings
                .AsNoTracking()
                .Where(s => s.GroupId == groupId)
                .Select(s => new { s.SettingsVersion })
                .FirstOrDefaultAsync(cancellationToken);

            var advancedSettings = await _dbContext.GroupAgentAdvancedSettings
                .AsNoTracking()
                .Where(s => s.GroupId == groupId)
                .Select(s => new { s.SettingsVersion })
                .FirstOrDefaultAsync(cancellationToken);

            var recentDevices = subtreeDevices
                .OrderByDescending(d => d.LastHeartbeatUtc.HasValue)
                .ThenByDescending(d => d.LastHeartbeatUtc)
                .Take(10)
                .Select(d => new DeviceManagerGroupRecentDeviceDto
                {
                    Id = d.Id,
                    HostName = d.HostName,
                    MacAddress = d.MacAddress,
                    Status = MapDeviceStatus(d),
                    LastHeartbeatUtc = d.LastHeartbeatUtc
                })
                .ToList();

            return new DeviceManagerGroupInfoDto
            {
                Id = group.Id,
                Name = group.Name,
                ParentId = group.ParentGroupId,
                ParentName = group.ParentName,
                IsDefault = group.IsDefault,
                SortOrder = group.SortOrder,
                CreatedUtc = group.CreatedUtc,
                Breadcrumb = BuildBreadcrumb(groupId, groupById),
                ChildGroups = childDtos,
                DirectChildGroupCount = childDtos.Count,
                TotalDevices = subtreeDevices.Count,
                OnlineCount = statusCounts.Online,
                OfflineCount = statusCounts.Offline,
                MaintenanceCount = statusCounts.Maintenance,
                StaleCount = statusCounts.Stale,
                HasRemoteSettings = remoteSettings is not null,
                HasAdvancedSettings = advancedSettings is not null,
                RemoteSettingsVersion = remoteSettings?.SettingsVersion,
                AdvancedSettingsVersion = advancedSettings?.SettingsVersion,
                RecentDevices = recentDevices
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await ExceptionLogHelper.SafeLogAsync(
                _exceptionLogWriter,
                _logger,
                $"{ServiceSource}.{nameof(GetGroupInfoAsync)}",
                ex,
                cancellationToken: cancellationToken);
            throw;
        }
    }

    public async Task<DeviceManagerDeviceInfoDto?> GetDeviceInfoAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = TenantDefaults.DefaultTenantId;
            var device = await _dbContext.Devices
                .AsNoTracking()
                .Include(d => d.Inventory)
                .Include(d => d.Group)
                .Include(d => d.RemoteSettings)
                .Include(d => d.AgentAdvancedSettings)
                .Where(d => d.Id == deviceId && d.TenantId == tenantId)
                .FirstOrDefaultAsync(cancellationToken);

            if (device is null)
            {
                return null;
            }

            IReadOnlyDictionary<Guid, GroupRow>? groupById = null;
            DeviceManagerDeviceGroupRefDto? groupRef = null;
            if (device.GroupId.HasValue && device.Group is not null)
            {
                var groups = await LoadGroupsAsync(tenantId, cancellationToken);
                groupById = groups.ToDictionary(g => g.Id);
                groupRef = new DeviceManagerDeviceGroupRefDto
                {
                    Id = device.Group.Id,
                    Name = device.Group.Name,
                    Breadcrumb = BuildBreadcrumb(device.Group.Id, groupById)
                };
            }

            DeviceManagerDeviceInventoryDto? inventoryDto = null;
            if (device.Inventory is not null)
            {
                inventoryDto = new DeviceManagerDeviceInventoryDto
                {
                    Hardware = DeviceManagerStatusHelper.TryParseJsonElement(device.Inventory.HardwareJson),
                    Network = DeviceManagerStatusHelper.TryParseJsonElement(device.Inventory.NetworkJson),
                    OsInfo = DeviceManagerStatusHelper.TryParseJsonElement(device.Inventory.OsInfoJson),
                    Security = DeviceManagerStatusHelper.TryParseJsonElement(device.Inventory.SecurityJson),
                    CollectedUtc = device.Inventory.CollectedUtc,
                    Version = device.Inventory.Version
                };
            }

            return new DeviceManagerDeviceInfoDto
            {
                Id = device.Id,
                HostName = device.HostName,
                MacAddress = device.MacAddress,
                Status = DeviceManagerStatusHelper.MapDeviceStatus(
                    device.EnrollmentState,
                    device.IsOnline,
                    device.ClientStatus,
                    device.LastHeartbeatUtc),
                BatteryPercent = DeviceManagerStatusHelper.TryParseBatteryPercent(
                    device.Inventory?.HardwareJson),
                AgentType = DeviceManagerStatusHelper.MapAgentType(device.Os),
                OsPlatform = device.Os,
                IsOnline = device.IsOnline,
                ClientStatus = device.ClientStatus,
                EnrollmentState = device.EnrollmentState,
                LastHeartbeatUtc = device.LastHeartbeatUtc,
                IpAddress = device.IpAddress,
                CommunicationIpAddress = device.CommunicationIpAddress,
                Domain = device.Domain,
                Workgroup = device.Workgroup,
                LoginUserName = device.LoginUserName,
                UserName = device.UserName,
                Os = device.Os,
                OsVersion = device.OsVersion,
                AgentVersion = device.AgentVersion,
                CommunicationType = device.CommunicationType,
                PollInterval = device.PollInterval,
                AgentUpTime = device.AgentUpTime,
                Duration = device.Duration,
                IsRegistered = device.IsRegistered,
                IsLicensed = device.IsLicensed,
                IsServiceMode = device.IsServiceMode,
                IsDhcp = device.IsDhcp,
                IsDomainJoined = device.IsDomainJoined,
                CreatedUtc = device.CreatedUtc,
                UpdatedUtc = device.UpdatedUtc,
                Group = groupRef,
                Inventory = inventoryDto,
                InheritFromGroup = device.RemoteSettings?.InheritFromGroup,
                RemoteSettingsPendingApply = device.RemoteSettings?.PendingApply ?? false,
                AdvancedSettingsPendingApply = device.AgentAdvancedSettings?.PendingApply ?? false
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await ExceptionLogHelper.SafeLogAsync(
                _exceptionLogWriter,
                _logger,
                $"{ServiceSource}.{nameof(GetDeviceInfoAsync)}",
                ex,
                cancellationToken: cancellationToken);
            throw;
        }
    }

    private Task<List<GroupRow>> LoadGroupsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _dbContext.DeviceGroups
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId)
            .Select(g => new GroupRow(
                g.Id,
                g.ParentGroupId,
                g.Name,
                g.SortOrder))
            .ToListAsync(cancellationToken);

    private Task<List<DeviceRow>> LoadDevicesAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _dbContext.Devices
            .AsNoTracking()
            .Include(d => d.Inventory)
            .Where(d => d.TenantId == tenantId)
            .Select(d => new DeviceRow(
                d.Id,
                d.GroupId,
                d.HostName,
                d.MacAddress,
                d.Os,
                d.IsOnline,
                d.ClientStatus,
                d.EnrollmentState,
                d.LastHeartbeatUtc,
                d.Inventory != null ? d.Inventory.HardwareJson : null))
            .ToListAsync(cancellationToken);

    private DeviceTreeResponse BuildTreeResponse(
        DeviceTreeQuery query,
        List<GroupRow> groups,
        List<DeviceRow> devices)
    {
        try
        {
            return BuildTreeResponseCore(query, groups, devices);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to build device manager tree (rootGroupId={RootGroupId}, status={Status}, hasSearch={HasSearch})",
                query.RootGroupId,
                query.Status,
                !string.IsNullOrWhiteSpace(query.Search));
            throw;
        }
    }

    private static DeviceTreeResponse BuildTreeResponseCore(
        DeviceTreeQuery query,
        List<GroupRow> groups,
        List<DeviceRow> devices)
    {
        var totalDeviceCount = devices.Count;

        if (query.RootGroupId.HasValue)
        {
            var subtreeIds = CollectSubtreeIds(groups, query.RootGroupId.Value);
            if (subtreeIds.Count == 0)
            {
                return new DeviceTreeResponse
                {
                    Items = [],
                    TotalDeviceCount = totalDeviceCount,
                    FilteredDeviceCount = 0
                };
            }

            groups = groups.Where(g => subtreeIds.Contains(g.Id)).ToList();
            devices = devices
                .Where(d => d.GroupId is null || subtreeIds.Contains(d.GroupId.Value))
                .ToList();
        }

        var statusFilter = NormalizeStatusFilter(query.Status);
        var searchTerm = string.IsNullOrWhiteSpace(query.Search)
            ? null
            : query.Search.Trim();
        var showEmptyGroups = statusFilter is null;

        var filteredDevices = ApplyDeviceStatusFilter(devices, statusFilter);
        var filteredDeviceCount = filteredDevices.Count;

        Dictionary<Guid, GroupRow> groupById;
        try
        {
            groupById = groups.ToDictionary(g => g.Id);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                "Device group data is inconsistent (duplicate group identifiers).",
                ex);
        }
        var groupHierarchy = BuildGroupHierarchy(groups);

        var devicesByGroup = filteredDevices
            .Where(d => d.GroupId.HasValue)
            .GroupBy(d => d.GroupId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(d => d.HostName, StringComparer.OrdinalIgnoreCase).ToList());

        var unassignedDevices = filteredDevices.Where(d => d.GroupId is null).ToList();

        IReadOnlyList<Guid> topLevelGroupIds;
        if (query.RootGroupId.HasValue)
        {
            topLevelGroupIds = [query.RootGroupId.Value];
        }
        else
        {
            topLevelGroupIds = groupHierarchy.RootGroups.Select(g => g.Id).ToList();
        }

        var items = new List<DeviceTreeNodeDto>();

        foreach (var groupId in topLevelGroupIds)
        {
            if (!groupById.TryGetValue(groupId, out var group))
            {
                continue;
            }

            var node = BuildGroupNode(
                group,
                group.ParentGroupId,
                depth: 0,
                groupHierarchy.ChildrenByParentId,
                devicesByGroup,
                showEmptyGroups);

            if (node is not null)
            {
                items.Add(node);
            }
        }

        if (query.IncludeUnassigned && unassignedDevices.Count > 0 &&
            (!query.RootGroupId.HasValue || query.RootGroupId == UnassignedNodeId))
        {
            var unassignedNode = BuildUnassignedNode(unassignedDevices, depth: 0);
            if (unassignedNode is not null)
            {
                items.Add(unassignedNode);
            }
        }

        if (searchTerm is not null)
        {
            items = PruneBySearch(items, searchTerm);
        }

        return new DeviceTreeResponse
        {
            Items = items,
            TotalDeviceCount = totalDeviceCount,
            FilteredDeviceCount = filteredDeviceCount
        };
    }

    private static GroupHierarchy BuildGroupHierarchy(IReadOnlyList<GroupRow> groups)
    {
        var hierarchy = new GroupHierarchy();
        foreach (var group in groups.OrderBy(g => g.SortOrder).ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (group.ParentGroupId is null)
            {
                hierarchy.RootGroups.Add(group);
                continue;
            }

            if (!hierarchy.ChildrenByParentId.TryGetValue(group.ParentGroupId.Value, out var siblings))
            {
                siblings = [];
                hierarchy.ChildrenByParentId[group.ParentGroupId.Value] = siblings;
            }

            siblings.Add(group);
        }

        return hierarchy;
    }

    private static HashSet<Guid> CollectSubtreeIds(IReadOnlyList<GroupRow> groups, Guid rootId)
    {
        var childrenByParent = groups
            .Where(g => g.ParentGroupId.HasValue)
            .GroupBy(g => g.ParentGroupId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        if (groups.All(g => g.Id != rootId))
        {
            return [];
        }

        var result = new HashSet<Guid> { rootId };
        var queue = new Queue<Guid>();
        queue.Enqueue(rootId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children))
            {
                continue;
            }

            foreach (var childId in children)
            {
                if (result.Add(childId))
                {
                    queue.Enqueue(childId);
                }
            }
        }

        return result;
    }

    private static List<DeviceRow> ApplyDeviceStatusFilter(IReadOnlyList<DeviceRow> devices, string? statusFilter)
    {
        if (statusFilter is null)
        {
            return devices.ToList();
        }

        return devices.Where(d =>
            DeviceManagerStatusHelper.DeviceStatusMatchesFilter(MapDeviceStatus(d), statusFilter)).ToList();
    }

    private static string? NormalizeStatusFilter(string status)
    {
        if (string.IsNullOrWhiteSpace(status) ||
            status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return status.Trim();
    }

    private static DeviceTreeNodeDto? BuildGroupNode(
        GroupRow group,
        Guid? parentId,
        int depth,
        IReadOnlyDictionary<Guid, List<GroupRow>> childrenByParentId,
        IReadOnlyDictionary<Guid, List<DeviceRow>> devicesByGroup,
        bool showEmptyGroups)
    {
        var childGroupNodes = new List<DeviceTreeNodeDto>();
        if (childrenByParentId.TryGetValue(group.Id, out var childGroups))
        {
            foreach (var childGroup in childGroups)
            {
                var childNode = BuildGroupNode(
                    childGroup,
                    group.Id,
                    depth + 1,
                    childrenByParentId,
                    devicesByGroup,
                    showEmptyGroups);
                if (childNode is not null)
                {
                    childGroupNodes.Add(childNode);
                }
            }
        }

        var deviceNodes = devicesByGroup.TryGetValue(group.Id, out var groupDevices)
            ? groupDevices.Select(d => MapDeviceNode(d, group.Id, depth + 1)).ToList()
            : [];

        var children = childGroupNodes.Concat(deviceNodes).ToList();
        if (!showEmptyGroups && children.Count == 0)
        {
            return null;
        }

        var aggregates = ComputeAggregates(children);

        return new DeviceTreeNodeDto
        {
            Id = group.Id,
            NodeType = parentId is null ? GroupNodeType : SubgroupNodeType,
            NodeName = group.Name,
            ParentId = parentId,
            Depth = depth,
            SortOrder = group.SortOrder,
            HasChildren = children.Count > 0,
            DeviceCount = aggregates.DeviceCount,
            OnlineCount = aggregates.OnlineCount,
            OfflineCount = aggregates.OfflineCount,
            MaintenanceCount = aggregates.MaintenanceCount,
            subRows = children
        };
    }

    private static DeviceTreeNodeDto MapDeviceNode(DeviceRow device, Guid parentId, int depth)
    {
        var status = MapDeviceStatus(device);
        return new DeviceTreeNodeDto
        {
            Id = device.Id,
            NodeType = DeviceNodeType,
            NodeName = device.HostName,
            ParentId = parentId,
            Depth = depth,
            SortOrder = 0,
            HasChildren = false,
            MacAddress = device.MacAddress,
            Status = status,
            BatteryPercent = DeviceManagerStatusHelper.TryParseBatteryPercent(device.HardwareJson),
            AgentType = DeviceManagerStatusHelper.MapAgentType(device.Os),
            OsPlatform = device.Os,
            IsOnline = DeviceManagerStatusHelper.IsDeviceOnline(device.IsOnline, device.ClientStatus),
            LastHeartbeatUtc = device.LastHeartbeatUtc,
            EnrollmentState = device.EnrollmentState
        };
    }

    private static DeviceTreeNodeDto? BuildUnassignedNode(IReadOnlyList<DeviceRow> devices, int depth)
    {
        var children = devices.Select(d => MapDeviceNode(d, UnassignedNodeId, depth + 1)).ToList();
        if (children.Count == 0)
        {
            return null;
        }

        var aggregates = ComputeAggregates(children);
        return new DeviceTreeNodeDto
        {
            Id = UnassignedNodeId,
            NodeType = GroupNodeType,
            NodeName = "Unassigned",
            ParentId = null,
            Depth = depth,
            SortOrder = int.MaxValue,
            HasChildren = true,
            DeviceCount = aggregates.DeviceCount,
            OnlineCount = aggregates.OnlineCount,
            OfflineCount = aggregates.OfflineCount,
            MaintenanceCount = aggregates.MaintenanceCount,
            subRows = children
        };
    }

    private static (int DeviceCount, int OnlineCount, int OfflineCount, int MaintenanceCount) ComputeAggregates(
        IReadOnlyList<DeviceTreeNodeDto> children)
    {
        var deviceCount = 0;
        var onlineCount = 0;
        var offlineCount = 0;
        var maintenanceCount = 0;

        foreach (var child in children)
        {
            if (child.NodeType == DeviceNodeType)
            {
                deviceCount++;
                switch (child.Status)
                {
                    case "Online":
                        onlineCount++;
                        break;
                    case "Maintenance":
                        maintenanceCount++;
                        break;
                    default:
                        offlineCount++;
                        break;
                }
            }
            else
            {
                deviceCount += child.DeviceCount ?? 0;
                onlineCount += child.OnlineCount ?? 0;
                offlineCount += child.OfflineCount ?? 0;
                maintenanceCount += child.MaintenanceCount ?? 0;
            }
        }

        return (deviceCount, onlineCount, offlineCount, maintenanceCount);
    }

    private static List<DeviceTreeNodeDto> PruneBySearch(
        IReadOnlyList<DeviceTreeNodeDto> nodes,
        string searchTerm)
    {
        var result = new List<DeviceTreeNodeDto>();
        foreach (var node in nodes)
        {
            var pruned = PruneNode(node, searchTerm);
            if (pruned is not null)
            {
                result.Add(pruned);
            }
        }

        return result;
    }

    private static DeviceTreeNodeDto? PruneNode(DeviceTreeNodeDto node, string searchTerm)
    {
        if (node.NodeType == DeviceNodeType)
        {
            return NodeMatchesSearch(node, searchTerm) ? node : null;
        }

        var prunedChildren = new List<DeviceTreeNodeDto>();
        if (node.subRows is not null)
        {
            foreach (var child in node.subRows)
            {
                var prunedChild = PruneNode(child, searchTerm);
                if (prunedChild is not null)
                {
                    prunedChildren.Add(prunedChild);
                }
            }
        }

        var selfMatches = NodeMatchesSearch(node, searchTerm);
        if (!selfMatches && prunedChildren.Count == 0)
        {
            return null;
        }

        if (prunedChildren.Count == 0)
        {
            return CloneGroupNode(node, []);
        }

        var aggregates = ComputeAggregates(prunedChildren);
        return CloneGroupNode(node, prunedChildren, aggregates);
    }

    private static DeviceTreeNodeDto CloneGroupNode(
        DeviceTreeNodeDto node,
        IReadOnlyList<DeviceTreeNodeDto> children,
        (int DeviceCount, int OnlineCount, int OfflineCount, int MaintenanceCount)? aggregates = null)
    {
        var stats = aggregates ?? ComputeAggregates(children);
        return new DeviceTreeNodeDto
        {
            Id = node.Id,
            NodeType = node.NodeType,
            NodeName = node.NodeName,
            ParentId = node.ParentId,
            Depth = node.Depth,
            SortOrder = node.SortOrder,
            HasChildren = children.Count > 0,
            DeviceCount = stats.DeviceCount,
            OnlineCount = stats.OnlineCount,
            OfflineCount = stats.OfflineCount,
            MaintenanceCount = stats.MaintenanceCount,
            subRows = children
        };
    }

    private static bool NodeMatchesSearch(DeviceTreeNodeDto node, string searchTerm)
    {
        return ContainsIgnoreCase(node.NodeName, searchTerm) ||
               (node.MacAddress is not null && ContainsIgnoreCase(node.MacAddress, searchTerm));
    }

    private static bool ContainsIgnoreCase(string value, string searchTerm) =>
        value.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);

    private static string MapDeviceStatus(DeviceRow device) =>
        DeviceManagerStatusHelper.MapDeviceStatus(
            device.EnrollmentState,
            device.IsOnline,
            device.ClientStatus,
            device.LastHeartbeatUtc);

    private static (int Online, int Offline, int Maintenance, int Stale) CountDeviceStatuses(
        IReadOnlyList<DeviceRow> devices)
    {
        var online = 0;
        var offline = 0;
        var maintenance = 0;
        var stale = 0;

        foreach (var device in devices)
        {
            switch (MapDeviceStatus(device))
            {
                case "Online":
                    online++;
                    break;
                case "Maintenance":
                    maintenance++;
                    break;
                case "Stale":
                    stale++;
                    break;
                default:
                    offline++;
                    break;
            }
        }

        return (online, offline, maintenance, stale);
    }

    private static List<string> BuildBreadcrumb(
        Guid groupId,
        IReadOnlyDictionary<Guid, GroupRow> groupById)
    {
        var path = new List<string>();
        var currentId = (Guid?)groupId;

        while (currentId.HasValue && groupById.TryGetValue(currentId.Value, out var group))
        {
            path.Add(group.Name);
            currentId = group.ParentGroupId;
        }

        path.Reverse();
        return path;
    }

    private sealed class GroupHierarchy
    {
        public List<GroupRow> RootGroups { get; } = [];
        public Dictionary<Guid, List<GroupRow>> ChildrenByParentId { get; } = [];
    }

    private sealed record GroupRow(Guid Id, Guid? ParentGroupId, string Name, int SortOrder);

    private sealed record GroupDetailRow(
        Guid Id,
        string Name,
        Guid? ParentGroupId,
        bool IsDefault,
        int SortOrder,
        DateTime CreatedUtc,
        string? ParentName);

    private sealed record DeviceRow(
        Guid Id,
        Guid? GroupId,
        string HostName,
        string MacAddress,
        string Os,
        bool IsOnline,
        string ClientStatus,
        EnrollmentState EnrollmentState,
        DateTime? LastHeartbeatUtc,
        string? HardwareJson);
}
