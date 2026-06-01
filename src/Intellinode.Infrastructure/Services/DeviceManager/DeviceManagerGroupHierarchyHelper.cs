namespace Intellinode.Infrastructure.Services.DeviceManager;

internal static class DeviceManagerGroupHierarchyHelper
{
    public static Dictionary<Guid, List<DeviceManagerGroupRow>> BuildChildrenByParentId(
        IReadOnlyList<DeviceManagerGroupRow> groups)
    {
        var childrenByParentId = new Dictionary<Guid, List<DeviceManagerGroupRow>>();

        foreach (var group in groups.Where(g => g.ParentGroupId.HasValue))
        {
            var parentId = group.ParentGroupId!.Value;
            if (!childrenByParentId.TryGetValue(parentId, out var siblings))
            {
                siblings = [];
                childrenByParentId[parentId] = siblings;
            }

            siblings.Add(group);
        }

        foreach (var siblings in childrenByParentId.Values)
        {
            siblings.Sort((a, b) =>
            {
                var order = a.SortOrder.CompareTo(b.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        return childrenByParentId;
    }

    public static HashSet<Guid> CollectSubtreeIds(IReadOnlyList<DeviceManagerGroupRow> groups, Guid rootId)
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

    public static int ResolveDepth(Guid groupId, IReadOnlyDictionary<Guid, DeviceManagerGroupRow> groupById)
    {
        var depth = 0;
        var currentId = (Guid?)groupId;

        while (currentId.HasValue && groupById.TryGetValue(currentId.Value, out var group))
        {
            if (group.ParentGroupId is null)
            {
                break;
            }

            depth++;
            currentId = group.ParentGroupId;
        }

        return depth;
    }

    public static List<string> BuildBreadcrumb(
        Guid groupId,
        IReadOnlyDictionary<Guid, DeviceManagerGroupRow> groupById)
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
}
