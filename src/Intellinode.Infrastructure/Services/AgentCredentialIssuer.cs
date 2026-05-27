using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Domain.Entities;
using Intellinode.Infrastructure.Options;
using Intellinode.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace Intellinode.Infrastructure.Services;

public sealed class AgentCredentialIssuer
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IDeviceRemoteSettingsService _remoteSettingsService;
    private readonly JwtOptions _options;

    public AgentCredentialIssuer(
        IntellinodeDbContext dbContext,
        ITokenService tokenService,
        IDeviceRemoteSettingsService remoteSettingsService,
        IOptions<JwtOptions> options)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _remoteSettingsService = remoteSettingsService;
        _options = options.Value;
    }

    public async Task<AgentAuthResponse> IssueAgentCredentialsAsync(
        Device device,
        CancellationToken cancellationToken = default)
    {
        var accessToken = _tokenService.CreateAgentAccessToken(device.Id, device.MacAddress);
        var refreshToken = _tokenService.CreateRefreshToken();
        var expiresUtc = DateTime.UtcNow.AddMinutes(_options.AgentTokenMinutes);

        _dbContext.AgentRefreshTokens.Add(new AgentRefreshToken
        {
            DeviceId = device.Id,
            TokenHash = _tokenService.HashToken(refreshToken),
            ExpiresUtc = DateTime.UtcNow.AddDays(_options.RefreshTokenDays)
        });

        var effective = await _remoteSettingsService.ResolveEffectiveForDeviceAsync(device.Id, cancellationToken);

        return new AgentAuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresUtc = expiresUtc,
            DeviceIdentity = device.MacAddress,
            Status = 1,
            ServerBaseUrl = effective.ServerBaseUrl,
            ApiBaseUrl = effective.ApiBaseUrl,
            PollIntervalSeconds = effective.PollIntervalSeconds
        };
    }
}
