using System.Security.Cryptography;
using System.Text.Json;
using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Options;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intellinode.Infrastructure.Services;

public sealed class AgentBootstrapService : IAgentBootstrapService
{
    private readonly IAgentServerUrlProvider _urlProvider;

    public AgentBootstrapService(IAgentServerUrlProvider urlProvider)
    {
        _urlProvider = urlProvider;
    }

    public AgentBootstrapResponse GetBootstrap() => _urlProvider.CreateBootstrapResponse();
}

public sealed class AgentEnrollmentService : IAgentEnrollmentService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly AgentCredentialIssuer _credentialIssuer;
    private readonly IAgentServerUrlProvider _urlProvider;
    private readonly AgentServerOptions _options;

    public AgentEnrollmentService(
        IntellinodeDbContext dbContext,
        ITokenService tokenService,
        AgentCredentialIssuer credentialIssuer,
        IAgentServerUrlProvider urlProvider,
        IOptions<AgentServerOptions> agentServerOptions)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _credentialIssuer = credentialIssuer;
        _urlProvider = urlProvider;
        _options = agentServerOptions.Value;
    }

    public async Task<AdminEnrollmentLinkResponse> CreateEnrollmentLinkAsync(
        Guid adminId,
        string? macAddress,
        CancellationToken cancellationToken = default)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var expiresUtc = DateTime.UtcNow.AddHours(_options.EnrollmentTokenValidityHours);
        _dbContext.AgentEnrollmentTokens.Add(new AgentEnrollmentToken
        {
            TokenHash = _tokenService.HashToken(token),
            MacAddress = string.IsNullOrWhiteSpace(macAddress) ? null : macAddress.Trim(),
            CreatedByAdminId = adminId,
            ExpiresUtc = expiresUtc
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AdminEnrollmentLinkResponse
        {
            Token = token,
            EnrollmentUrl = _urlProvider.BuildEnrollmentUrl(token),
            ExpiresUtc = expiresUtc
        };
    }

    public async Task<AgentEnrollResult> EnrollAsync(
        AgentEnrollRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = _tokenService.HashToken(request.Token.Trim());
        var enrollment = await _dbContext.AgentEnrollmentTokens
            .FirstOrDefaultAsync(
                t => t.TokenHash == tokenHash && t.ConsumedUtc == null,
                cancellationToken);

        if (enrollment is null || enrollment.ExpiresUtc < DateTime.UtcNow)
        {
            return AgentEnrollResult.Failure(
                "InvalidEnrollmentToken",
                "The enrollment token is invalid, expired, or has already been used.");
        }

        var (macAddress, macError) = ResolveMacAddress(request, enrollment);
        if (macAddress is null)
        {
            return macError == "MacMismatch"
                ? AgentEnrollResult.Failure(
                    "MacMismatch",
                    "The device identity does not match the MAC address bound to this enrollment token.")
                : AgentEnrollResult.Failure(
                    "InvalidEnrollmentToken",
                    "A valid device identity is required to complete enrollment.");
        }

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
                IsRegistered = true,
                EnrollmentState = EnrollmentState.PendingInventory,
                GroupId = defaultGroup?.Id
            };
            _dbContext.Devices.Add(device);
        }
        else
        {
            device.IsRegistered = true;
            device.UpdatedUtc = DateTime.UtcNow;
        }

        enrollment.ConsumedUtc = DateTime.UtcNow;
        enrollment.DeviceId = device.Id;

        var response = await _credentialIssuer.IssueAgentCredentialsAsync(device, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return AgentEnrollResult.Success(response);
    }

    private static (string? MacAddress, string? ErrorCode) ResolveMacAddress(
        AgentEnrollRequest request,
        AgentEnrollmentToken enrollment)
    {
        var requestedMac = request.DeviceIdentity?.Trim();
        if (!string.IsNullOrWhiteSpace(enrollment.MacAddress))
        {
            if (!string.IsNullOrWhiteSpace(requestedMac) &&
                !string.Equals(requestedMac, enrollment.MacAddress, StringComparison.OrdinalIgnoreCase))
            {
                return (null, "MacMismatch");
            }

            return (enrollment.MacAddress, null);
        }

        return string.IsNullOrWhiteSpace(requestedMac) ? (null, "InvalidEnrollmentToken") : (requestedMac, null);
    }
}

public sealed class AgentInventoryService : IAgentInventoryService
{
    private readonly IntellinodeDbContext _dbContext;

    public AgentInventoryService(IntellinodeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpsertInventoryAsync(
        Guid deviceId,
        AgentInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var device = await _dbContext.Devices
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);

        if (device is null)
        {
            throw new InvalidOperationException($"Device '{deviceId}' is not registered.");
        }

        var hardware = ToJsonParameter(request.Hardware);
        var network = ToJsonParameter(request.Network);
        var osInfo = ToJsonParameter(request.OsInfo);
        var security = ToJsonParameter(request.Security);

        var existing = await _dbContext.DeviceInventories
            .FirstOrDefaultAsync(i => i.DeviceId == deviceId, cancellationToken);

        if (existing is null)
        {
            _dbContext.DeviceInventories.Add(new DeviceInventory
            {
                DeviceId = deviceId,
                HardwareJson = hardware,
                NetworkJson = network,
                OsInfoJson = osInfo,
                SecurityJson = security
            });
        }
        else
        {
            existing.HardwareJson = hardware ?? existing.HardwareJson;
            existing.NetworkJson = network ?? existing.NetworkJson;
            existing.OsInfoJson = osInfo ?? existing.OsInfoJson;
            existing.SecurityJson = security ?? existing.SecurityJson;
            existing.CollectedUtc = DateTime.UtcNow;
            existing.Version += 1;
        }

        device.EnrollmentState = EnrollmentState.Active;
        device.IsRegistered = true;
        device.UpdatedUtc = DateTime.UtcNow;
        InventoryFieldMapper.ApplyToDevice(device, request);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? ToJsonParameter(JsonElement? element)
    {
        if (!element.HasValue)
        {
            return null;
        }

        var value = element.Value;
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.GetRawText();
    }
}
