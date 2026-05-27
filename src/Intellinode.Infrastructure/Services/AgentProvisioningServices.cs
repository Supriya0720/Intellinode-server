using System.Security.Cryptography;
using System.Text.Json;
using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
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
    private readonly IAgentServerUrlProvider _urlProvider;
    private readonly AgentServerOptions _options;

    public AgentEnrollmentService(
        IntellinodeDbContext dbContext,
        ITokenService tokenService,
        IAgentServerUrlProvider urlProvider,
        IOptions<AgentServerOptions> agentServerOptions)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
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
            Platform = AgentPlatform.Windows,
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
        var device = await FindDeviceAsync(deviceId, cancellationToken);

        if (device is null)
        {
            throw new InvalidOperationException($"Device '{deviceId}' is not registered.");
        }

        ApplyInventory(device, request);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyInventoryAsync(
        Guid deviceId,
        AgentInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var device = await FindDeviceAsync(deviceId, cancellationToken);

        if (device is null)
        {
            throw new InvalidOperationException($"Device '{deviceId}' is not registered.");
        }

        ApplyInventory(device, request);
    }

    private async Task<Device?> FindDeviceAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var device = _dbContext.Devices.Local.FirstOrDefault(d => d.Id == deviceId);
        if (device is not null)
        {
            return device;
        }

        return await _dbContext.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);
    }

    private void ApplyInventory(Device device, AgentInventoryRequest request)
    {
        var hardware = ToJsonParameter(request.Hardware);
        var network = ToJsonParameter(request.Network);
        var osInfo = ToJsonParameter(request.OsInfo);
        var security = ToJsonParameter(request.Security);

        var existing = _dbContext.DeviceInventories
            .Local
            .FirstOrDefault(i => i.DeviceId == device.Id)
            ?? _dbContext.DeviceInventories
            .FirstOrDefault(i => i.DeviceId == device.Id);

        if (existing is null)
        {
            _dbContext.DeviceInventories.Add(new DeviceInventory
            {
                DeviceId = device.Id,
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
