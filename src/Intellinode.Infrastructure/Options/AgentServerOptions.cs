namespace Intellinode.Infrastructure.Options;

public sealed class AgentServerOptions
{
    public const string SectionName = "AgentServer";

    /// <summary>
    /// Public agent-facing base URL (FusionX WebServerAddress equivalent), e.g. https://uem.example.com
    /// </summary>
    public string ServerBaseUrl { get; set; } = "https://localhost:5288";

    /// <summary>
    /// REST API base including version segment. When empty, derived from <see cref="ServerBaseUrl"/> + /api/v1.
    /// </summary>
    public string? ApiBaseUrl { get; set; }

    public int DefaultPollIntervalSeconds { get; set; } = 300;

    public int EnrollmentTokenValidityHours { get; set; } = 24;
}
