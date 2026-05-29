namespace Intellinode.Infrastructure.Options;

public sealed class AgentDiscoveryOptions
{
    public const string SectionName = "AgentDiscovery";

    /// <summary>When true, self-discovery inventory requires admin approval (FusionX default).</summary>
    public bool RequireAdminApproval { get; set; } = true;

    /// <summary>Retention hint for stale pending discovery rows (future cleanup jobs).</summary>
    public int PendingDiscoveryRetentionDays { get; set; } = 90;

    /// <summary>When true, a rejected device may upload inventory again to re-enter the pending queue.</summary>
    public bool AllowReDiscoveryAfterReject { get; set; } = true;

    /// <summary>Legacy plain-text heartbeat maps internal "exists" to FusionX wire value "2"; JSON always uses "exists".</summary>
    public bool LegacyMapExistsToTwo { get; set; } = true;
}
