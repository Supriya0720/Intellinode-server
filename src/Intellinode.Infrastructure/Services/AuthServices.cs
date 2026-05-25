using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Infrastructure.Options;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Intellinode.Infrastructure.Services;

public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string CreateAgentAccessToken(Guid deviceId, string macAddress)
    {
        return CreateToken(
            [
                new Claim(JwtRegisteredClaimNames.Sub, deviceId.ToString()),
                new Claim("mac", macAddress),
                new Claim(ClaimTypes.Role, "Agent")
            ],
            _options.AgentTokenMinutes);
    }

    public string CreateAdminAccessToken(Guid adminId, string userName)
    {
        return CreateToken(
            [
                new Claim(JwtRegisteredClaimNames.Sub, adminId.ToString()),
                new Claim(ClaimTypes.Name, userName),
                new Claim(ClaimTypes.Role, "Admin")
            ],
            _options.AdminTokenMinutes);
    }

    public string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private string CreateToken(IEnumerable<Claim> claims, int lifetimeMinutes)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(lifetimeMinutes);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class AgentAuthService : IAgentAuthService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly AgentCredentialIssuer _credentialIssuer;
    private readonly ITokenService _tokenService;

    public AgentAuthService(
        IntellinodeDbContext dbContext,
        AgentCredentialIssuer credentialIssuer,
        ITokenService tokenService)
    {
        _dbContext = dbContext;
        _credentialIssuer = credentialIssuer;
        _tokenService = tokenService;
    }

    public async Task<AgentAuthResponse> AuthenticateAsync(
        AgentAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        var macAddress = request.DeviceIdentity.Trim();
        var device = await _dbContext.Devices.FirstOrDefaultAsync(
            d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == macAddress,
            cancellationToken);

        if (device is null)
        {
            var defaultGroup = await _dbContext.DeviceGroups.FirstOrDefaultAsync(
                g => g.TenantId == TenantDefaults.DefaultTenantId && g.IsDefault,
                cancellationToken);
            device = new Device
            {
                TenantId = TenantDefaults.DefaultTenantId,
                MacAddress = macAddress,
                IsRegistered = request.IsRegistered == 1,
                EnrollmentState = Domain.Enums.EnrollmentState.PendingInventory,
                GroupId = defaultGroup?.Id
            };
            _dbContext.Devices.Add(device);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            device.IsRegistered = request.IsRegistered == 1;
            device.UpdatedUtc = DateTime.UtcNow;
        }

        var response = await _credentialIssuer.IssueAgentCredentialsAsync(device, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<AgentRefreshResult> RefreshAsync(
        AgentRefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return AgentRefreshResult.Failure(
                "InvalidRefreshToken",
                "The refresh token is invalid.");
        }

        var tokenHash = _tokenService.HashToken(request.RefreshToken.Trim());
        var stored = await _dbContext.AgentRefreshTokens
            .Include(t => t.Device)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (stored is null || stored.Device is null)
        {
            return AgentRefreshResult.Failure(
                "InvalidRefreshToken",
                "The refresh token is invalid.");
        }

        if (stored.RevokedUtc is not null)
        {
            return AgentRefreshResult.Failure(
                "RefreshTokenRevoked",
                "The refresh token has been revoked.");
        }

        if (stored.ExpiresUtc < DateTime.UtcNow)
        {
            return AgentRefreshResult.Failure(
                "RefreshTokenExpired",
                "The refresh token has expired.");
        }

        stored.RevokedUtc = DateTime.UtcNow;
        var response = await _credentialIssuer.IssueAgentCredentialsAsync(stored.Device, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return AgentRefreshResult.Success(response);
    }

    public async Task RevokeRefreshTokenAsync(
        AgentRevokeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var tokenHash = _tokenService.HashToken(request.RefreshToken.Trim());
        var stored = await _dbContext.AgentRefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (stored is not null && stored.RevokedUtc is null)
        {
            stored.RevokedUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class AdminAuthService : IAdminAuthService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _options;

    public AdminAuthService(
        IntellinodeDbContext dbContext,
        ITokenService tokenService,
        IOptions<JwtOptions> options)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _options = options.Value;
    }

    public async Task<AdminLoginResponse?> LoginAsync(
        AdminLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var admin = await _dbContext.AdminUsers
            .FirstOrDefaultAsync(x => x.UserName == request.UserName && x.IsActive, cancellationToken);

        if (admin is null || !BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash))
        {
            return null;
        }

        return new AdminLoginResponse
        {
            AccessToken = _tokenService.CreateAdminAccessToken(admin.Id, admin.UserName),
            ExpiresUtc = DateTime.UtcNow.AddMinutes(_options.AdminTokenMinutes),
            UserName = admin.UserName
        };
    }
}
