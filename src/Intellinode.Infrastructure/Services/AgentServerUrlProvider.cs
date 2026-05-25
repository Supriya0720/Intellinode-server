using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Intellinode.Infrastructure.Services;

public sealed class AgentServerUrlProvider : IAgentServerUrlProvider
{
    private readonly AgentServerOptions _options;

    public AgentServerUrlProvider(IOptions<AgentServerOptions> options)
    {
        _options = options.Value;
        ServerBaseUrl = NormalizeBaseUrl(_options.ServerBaseUrl);
        ApiBaseUrl = string.IsNullOrWhiteSpace(_options.ApiBaseUrl)
            ? $"{ServerBaseUrl}/api/v1"
            : NormalizeBaseUrl(_options.ApiBaseUrl);
        DefaultPollIntervalSeconds = _options.DefaultPollIntervalSeconds;
    }

    public string ServerBaseUrl { get; }
    public string ApiBaseUrl { get; }
    public int DefaultPollIntervalSeconds { get; }

    public AgentBootstrapResponse CreateBootstrapResponse() => new()
    {
        ServerBaseUrl = ServerBaseUrl,
        ApiBaseUrl = ApiBaseUrl,
        DefaultPollIntervalSeconds = DefaultPollIntervalSeconds,
        Endpoints = new AgentEndpointPaths()
    };

    public void ApplyProvisioningUrls(AgentAuthResponse response)
    {
        response.ServerBaseUrl = ServerBaseUrl;
        response.ApiBaseUrl = ApiBaseUrl;
        response.PollIntervalSeconds = DefaultPollIntervalSeconds;
    }

    public string BuildEnrollmentUrl(string token) =>
        $"{ServerBaseUrl}/enroll?token={Uri.EscapeDataString(token)}";

    private static string NormalizeBaseUrl(string url) => url.TrimEnd('/');
}
