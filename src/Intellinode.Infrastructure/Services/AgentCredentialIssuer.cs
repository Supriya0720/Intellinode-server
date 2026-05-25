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
    private readonly IAgentServerUrlProvider _urlProvider;
    private readonly JwtOptions _options;

    public AgentCredentialIssuer(
        IntellinodeDbContext dbContext,
        ITokenService tokenService,
        IAgentServerUrlProvider urlProvider,
        IOptions<JwtOptions> options)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _urlProvider = urlProvider;
        _options = options.Value;
    }

    public Task<AgentAuthResponse> IssueAgentCredentialsAsync(
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

        var response = new AgentAuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresUtc = expiresUtc,
            DeviceIdentity = device.MacAddress,
            Status = 1
        };

        _urlProvider.ApplyProvisioningUrls(response);
        return Task.FromResult(response);
    }
}
