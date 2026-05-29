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
    private readonly IDiscoverLookupWriter _discoverLookupWriter;
    private readonly IAgentCommunicationLogWriter _communicationLogWriter;
    private readonly AgentDiscoveryOptions _discoveryOptions;

    public AgentInventoryService(
        IntellinodeDbContext dbContext,
        IDiscoverLookupWriter discoverLookupWriter,
        IAgentCommunicationLogWriter communicationLogWriter,
        IOptions<AgentDiscoveryOptions> discoveryOptions)
    {
        _dbContext = dbContext;
        _discoverLookupWriter = discoverLookupWriter;
        _communicationLogWriter = communicationLogWriter;
        _discoveryOptions = discoveryOptions.Value;
    }

    public async Task<AgentInventorySubmitResponse> UpsertInventoryAsync(
        Guid deviceId,
        AgentInventoryRequest request,
        InventorySubmissionKind kind = InventorySubmissionKind.SelfDiscovery,
        CancellationToken cancellationToken = default)
    {
        var device = await FindDeviceAsync(deviceId, cancellationToken);

        if (device is null)
        {
            throw new InvalidOperationException($"Device '{deviceId}' is not registered.");
        }

        var response = await ApplyInventoryToDeviceAsync(device, request, kind, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task ApplyInventoryAsync(
        Guid deviceId,
        AgentInventoryRequest request,
        InventorySubmissionKind kind = InventorySubmissionKind.TokenEnrollment,
        CancellationToken cancellationToken = default)
    {
        var device = await FindDeviceAsync(deviceId, cancellationToken);

        if (device is null)
        {
            throw new InvalidOperationException($"Device '{deviceId}' is not registered.");
        }

        await ApplyInventoryToDeviceAsync(device, request, kind, cancellationToken);
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

    private async Task<AgentInventorySubmitResponse> ApplyInventoryToDeviceAsync(
        Device device,
        AgentInventoryRequest request,
        InventorySubmissionKind kind,
        CancellationToken cancellationToken)
    {
        UpsertInventoryJson(device, request);
        InventoryFieldMapper.ApplyToDevice(device, request);
        device.UpdatedUtc = DateTime.UtcNow;

        switch (kind)
        {
            case InventorySubmissionKind.SelfDiscovery when _discoveryOptions.RequireAdminApproval:
                if (device.EnrollmentState == EnrollmentState.Rejected &&
                    !_discoveryOptions.AllowReDiscoveryAfterReject)
                {
                    return new AgentInventorySubmitResponse
                    {
                        ErrorCode = "DeviceRejected",
                        Message = "Device discovery was rejected and re-discovery is disabled."
                    };
                }

                if (DeviceEnrollmentGuard.IsManaged(device.EnrollmentState))
                {
                    await _discoverLookupWriter.UpsertPendingFromInventoryAsync(device, request, cancellationToken);

                    await _communicationLogWriter.LogAsync(
                        device.Id,
                        device.MacAddress,
                        "Inbound",
                        "/api/v1/agents/inventory",
                        device.EnrollmentState.ToString(),
                        "Inventory resync for managed device",
                        cancellationToken);

                    return new AgentInventorySubmitResponse
                    {
                        Status = device.EnrollmentState.ToString(),
                        Message = "Device inventory updated."
                    };
                }

                if (device.EnrollmentState == EnrollmentState.PendingApproval)
                {
                    await _discoverLookupWriter.UpsertPendingFromInventoryAsync(device, request, cancellationToken);

                    await _communicationLogWriter.LogAsync(
                        device.Id,
                        device.MacAddress,
                        "Inbound",
                        "/api/v1/agents/inventory",
                        "PendingApproval",
                        "Self-discovery inventory refresh",
                        cancellationToken);

                    return new AgentInventorySubmitResponse
                    {
                        Status = "PendingApproval",
                        Message = "Device discovered. Awaiting administrator approval."
                    };
                }

                var isReDiscovery = device.EnrollmentState == EnrollmentState.Rejected;
                device.EnrollmentState = EnrollmentState.PendingApproval;
                device.IsRegistered = false;
                await _discoverLookupWriter.UpsertPendingFromInventoryAsync(device, request, cancellationToken);

                await _communicationLogWriter.LogAsync(
                    device.Id,
                    device.MacAddress,
                    "Inbound",
                    "/api/v1/agents/inventory",
                    "PendingApproval",
                    isReDiscovery ? "Re-discovery inventory" : "Self-discovery inventory",
                    cancellationToken);

                return new AgentInventorySubmitResponse
                {
                    Status = "PendingApproval",
                    Message = "Device discovered. Awaiting administrator approval."
                };

            case InventorySubmissionKind.SelfDiscovery:
                device.EnrollmentState = EnrollmentState.Active;
                device.IsRegistered = true;
                return new AgentInventorySubmitResponse
                {
                    Status = "Active",
                    Message = "Device inventory recorded and activated."
                };

            case InventorySubmissionKind.TokenEnrollment:
                device.EnrollmentState = EnrollmentState.Active;
                device.IsRegistered = true;
                return new AgentInventorySubmitResponse
                {
                    Status = "Active",
                    Message = "Device enrolled and activated."
                };

            case InventorySubmissionKind.Resync:
            default:
                return new AgentInventorySubmitResponse
                {
                    Status = device.EnrollmentState.ToString(),
                    Message = "Device inventory updated."
                };
        }
    }

    private void UpsertInventoryJson(Device device, AgentInventoryRequest request)
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
