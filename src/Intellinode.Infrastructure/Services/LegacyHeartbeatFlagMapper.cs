namespace Intellinode.Infrastructure.Services;

public static class LegacyHeartbeatFlagMapper
{
    public static string ToWireFormat(string autoDiscoverFlag, bool mapExistsToTwo) =>
        mapExistsToTwo && string.Equals(autoDiscoverFlag, "exists", StringComparison.Ordinal)
            ? "2"
            : autoDiscoverFlag;
}
