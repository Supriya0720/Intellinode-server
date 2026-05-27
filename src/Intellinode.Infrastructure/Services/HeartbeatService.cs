using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services;

public sealed class HeartbeatService : IHeartbeatService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly IEffectiveAgentSettingsResolver _settingsResolver;
    private readonly ILogger<HeartbeatService> _logger;

    public HeartbeatService(
        IntellinodeDbContext dbContext,
        IEffectiveAgentSettingsResolver settingsResolver,
        ILogger<HeartbeatService> logger)
    {
        _dbContext = dbContext;
        _settingsResolver = settingsResolver;
        _logger = logger;
    }

    public async Task<HeartbeatResponse> ProcessHeartbeatAsync(
        AgentClientStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var macAddress = request.MacAddress.Trim();
        var device = await _dbContext.Devices
            .Include(d => d.Group)
            .Include(d => d.Tasks)
            .Include(d => d.Inventory)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == macAddress,
                cancellationToken);

        var lastHeartbeatUtc = DateTime.UtcNow;

        if (device is null)
        {
            return new HeartbeatResponse
            {
                AutoDiscoverFlag = "SDFT",
                LastHeartbeatUtc = lastHeartbeatUtc
            };
        }

        var (clientStatus, isServiceMode) = ParseClientStatus(request.ClientStatus);

        var earlyResponse = await TryProcessEarlyAcknowledgementsAsync(
            device,
            request,
            clientStatus,
            isServiceMode,
            lastHeartbeatUtc,
            cancellationToken);
        if (earlyResponse is not null)
        {
            return earlyResponse;
        }

        var ipAddresses = request.IpAddress.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var response = new HeartbeatResponse
        {
            AutoDiscoverFlag = "exists",
            LastHeartbeatUtc = lastHeartbeatUtc
        };

        device.IsServiceMode = isServiceMode;
        device.ClientStatus = clientStatus;
        device.CommunicationType = request.CommunicationType ?? string.Empty;
        device.PollInterval = request.PollInterval;
        device.AgentUpTime = request.AgentUpTime ?? string.Empty;
        device.Duration = request.Duration ?? string.Empty;
        device.IsDhcp = request.Dhcp;
        device.CommunicationIpAddress = request.CommunicationIpAddress ?? string.Empty;
        device.SubnetMask = request.SubnetMask ?? string.Empty;
        device.Gateway = request.Gateway ?? string.Empty;
        device.PrimaryDns = request.PrimaryDns ?? string.Empty;
        device.SecondaryDns = request.SecondaryDns ?? string.Empty;
        device.PrimaryWins = NormalizeWins(request.PrimaryWins);
        device.SecondaryWins = NormalizeWins(request.SecondaryWins);
        device.Domain = request.Domain ?? string.Empty;
        device.Workgroup = request.Workgroup ?? string.Empty;
        device.IsDomainJoined = request.IsDomainJoined;
        device.LoginUserName = NormalizeLoginUserName(request.LoginUserName);
        device.UserName = request.UserName ?? string.Empty;
        device.LicenseKey = request.License ?? string.Empty;
        device.LastHeartbeatUtc = response.LastHeartbeatUtc;
        device.UpdatedUtc = response.LastHeartbeatUtc;
        device.IsOnline = clientStatus == ClientPowerStatus.On;

        string? clientUpdateStatus = null;

        if (ipAddresses.Length > 1)
        {
            var primaryIp = ipAddresses[0];
            var bindingActive = await RecordBindingChangeAsync(
                device,
                ipAddresses.Length,
                isServiceMode,
                clientStatus,
                primaryIp,
                HeartbeatBindingKind.IpAddress,
                cancellationToken);

            if (!bindingActive)
            {
                var ipUpdate = await UpdateDeviceIpAddressAsync(device, primaryIp, request.Dhcp, clientStatus == ClientPowerStatus.On, cancellationToken);
                if (string.IsNullOrWhiteSpace(request.HostName))
                {
                    response.HostName = ipUpdate.HostName;
                    request.HostName = ipUpdate.HostName;
                }

                if (ipUpdate.UpdateStatus is "Update" or "CHANGE")
                {
                    device.IpAddress = primaryIp;
                    response.IpAddress = primaryIp;
                }

                if (clientStatus == ClientPowerStatus.Coff)
                {
                    device.IpAddress = primaryIp;
                    response.IpAddress = primaryIp;
                }
            }
        }
        else if (ipAddresses.Length == 1)
        {
            await RecordBindingChangeAsync(
                device,
                ipAddresses.Length,
                isServiceMode,
                clientStatus,
                request.HostName.Trim(),
                HeartbeatBindingKind.HostName,
                cancellationToken);

            clientUpdateStatus = await UpdateClientStatusAsync(device, request, clientStatus, cancellationToken);
            response.ClientUpdateStatus = clientUpdateStatus;

            if (clientUpdateStatus == ClientUpdateStatus.NoChange)
            {
                device.IpAddress = ipAddresses[0];
                response.IpAddress = ipAddresses[0];
            }
        }

        response.AutoDiscoverFlag = await ResolveAutoDiscoverFlagAsync(device, request, clientUpdateStatus, cancellationToken);
        await ProcessAgentAcknowledgementsAsync(device, request, clientStatus, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        response.ConfigPending = await _settingsResolver.HasPendingConfigAsync(device.Id, cancellationToken);
        return response;
    }

    private async Task<HeartbeatResponse?> TryProcessEarlyAcknowledgementsAsync(
        Device device,
        AgentClientStatusRequest request,
        string clientStatus,
        bool isServiceMode,
        DateTime lastHeartbeatUtc,
        CancellationToken cancellationToken)
    {
        if (string.Equals(clientStatus, ClientPowerStatus.Coff, StringComparison.OrdinalIgnoreCase))
        {
            device.IsServiceMode = isServiceMode;
            device.ClientStatus = ClientPowerStatus.Coff;
            device.IsOnline = false;
            device.LastHeartbeatUtc = lastHeartbeatUtc;
            device.UpdatedUtc = lastHeartbeatUtc;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new HeartbeatResponse
            {
                AutoDiscoverFlag = "0",
                LastHeartbeatUtc = lastHeartbeatUtc,
                ConfigPending = await _settingsResolver.HasPendingConfigAsync(device.Id, cancellationToken)
            };
        }

        if (request.ShutdownAck is ShutdownAcknowledgement.Shutdown or ShutdownAcknowledgement.Restart)
        {
            await ProcessAgentAcknowledgementsAsync(
                device,
                request,
                clientStatus,
                cancellationToken,
                requireSingleIp: false);
            device.LastHeartbeatUtc = lastHeartbeatUtc;
            device.UpdatedUtc = lastHeartbeatUtc;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new HeartbeatResponse
            {
                AutoDiscoverFlag = "1",
                LastHeartbeatUtc = lastHeartbeatUtc,
                ConfigPending = await _settingsResolver.HasPendingConfigAsync(device.Id, cancellationToken)
            };
        }

        return null;
    }

    private static (string ClientStatus, bool IsServiceMode) ParseClientStatus(string rawStatus)
    {
        var normalized = rawStatus.Trim().ToUpperInvariant();
        if (!normalized.Contains('~'))
        {
            return (normalized, false);
        }

        var parts = normalized.Split('~', 2, StringSplitOptions.TrimEntries);
        var serviceMode = parts.Length > 1 && parts[1] == "S";
        return (parts[0], serviceMode);
    }

    private async Task<bool> RecordBindingChangeAsync(
        Device device,
        int ipCount,
        bool isServiceMode,
        string status,
        string changedValue,
        HeartbeatBindingKind kind,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.HeartbeatBindingChanges
            .Where(x => x.DeviceId == device.Id && x.IsBindingActive)
            .OrderByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null &&
            existing.Status == status &&
            existing.ChangedValue == changedValue &&
            existing.IsServiceMode == isServiceMode &&
            existing.Kind == kind)
        {
            // Legacy @Binding=true: active binding still in progress, skip IP update.
            return true;
        }

        if (kind == HeartbeatBindingKind.IpAddress &&
            string.Equals(device.IpAddress, changedValue, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (kind == HeartbeatBindingKind.HostName &&
            string.Equals(device.HostName, changedValue, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (existing is not null)
        {
            existing.IsBindingActive = false;
        }

        _dbContext.HeartbeatBindingChanges.Add(new HeartbeatBindingChange
        {
            DeviceId = device.Id,
            IsServiceMode = isServiceMode,
            Status = status,
            ChangedValue = changedValue,
            Kind = kind,
            IsBindingActive = true
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<(string UpdateStatus, string HostName, string ClientStatus)> UpdateDeviceIpAddressAsync(
        Device device,
        string ipAddress,
        bool isDhcp,
        bool fromClient,
        CancellationToken cancellationToken)
    {
        var previousIp = device.IpAddress;
        var previousHostName = device.HostName;
        var clientStatus = "SAME";

        if (!string.Equals(previousIp, ipAddress, StringComparison.OrdinalIgnoreCase))
        {
            device.IpAddress = ipAddress;
            device.IsDhcp = isDhcp;
            clientStatus = fromClient ? "CHANGE" : "UPDATE";
        }

        if (string.IsNullOrWhiteSpace(device.HostName))
        {
            device.HostName = previousHostName;
        }

        await Task.CompletedTask;
        return (clientStatus == "SAME" ? "NoUpdate" : "Update", device.HostName, clientStatus);
    }

    private async Task<string> UpdateClientStatusAsync(
        Device device,
        AgentClientStatusRequest request,
        string clientStatus,
        CancellationToken cancellationToken)
    {
        device.HostName = string.IsNullOrWhiteSpace(request.HostName) ? device.HostName : request.HostName.Trim();
        device.IpAddress = request.IpAddress.Split(',')[0].Trim();

        if (clientStatus is ClientPowerStatus.Off or ClientPowerStatus.Coff)
        {
            device.IsOnline = false;
            return ClientUpdateStatus.NoChange;
        }

        if (clientStatus != ClientPowerStatus.On)
        {
            device.IsOnline = false;
            return ClientUpdateStatus.NoChange;
        }

        var requestedGroup = request.GroupName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(requestedGroup))
        {
            ApplyClientInventory(device, request);
            return ClientUpdateStatus.NoChange;
        }

        var group = await _dbContext.DeviceGroups
            .FirstOrDefaultAsync(
                g => g.TenantId == TenantDefaults.DefaultTenantId && g.Name == requestedGroup,
                cancellationToken);

        if (group is null)
        {
            ApplyClientInventory(device, request);
            return ClientUpdateStatus.NoChange;
        }

        if (device.Group?.Name == requestedGroup)
        {
            ApplyClientInventory(device, request);
            return ClientUpdateStatus.NoChange;
        }

        if (requestedGroup.Equals("Root", StringComparison.OrdinalIgnoreCase))
        {
            var defaultGroup = await _dbContext.DeviceGroups.FirstOrDefaultAsync(
                g => g.TenantId == TenantDefaults.DefaultTenantId && g.IsDefault,
                cancellationToken);
            if (defaultGroup is not null)
            {
                device.GroupId = defaultGroup.Id;
            }
        }
        else
        {
            device.GroupId = group.Id;
        }

        ApplyClientInventory(device, request);
        return ClientUpdateStatus.Changed;
    }

    private static void ApplyClientInventory(Device device, AgentClientStatusRequest request)
    {
        device.HostName = string.IsNullOrWhiteSpace(request.HostName) ? device.HostName : request.HostName.Trim();
        device.IpAddress = request.IpAddress.Split(',')[0].Trim();
        device.IsOnline = true;
    }

    private async Task<string> ResolveAutoDiscoverFlagAsync(
        Device device,
        AgentClientStatusRequest request,
        string? clientUpdateStatus,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CommunicationType))
        {
            return "NOK";
        }

        if (!string.Equals(request.CommunicationType, "HTTP", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveDiscoverLookupFlagAsync(device, cancellationToken);
        }

        try
        {
            if (await RequiresInventoryDiscoveryAsync(device, cancellationToken))
            {
                return "SDFT";
            }

            var pendingTasks = device.Tasks
                .Where(t => t.Status is DeviceTaskStatus.Pending or DeviceTaskStatus.InProcess)
                .ToList();

            if (pendingTasks.Count > 0)
            {
                return "1";
            }

            var hasSpecialTasks = await _dbContext.DeviceTasks
                .AnyAsync(
                    t => t.DeviceId == device.Id &&
                         (t.FunctionName == "Get_FBWF_UWF_Status" || t.Status == DeviceTaskStatus.InProcess),
                    cancellationToken);

            if (hasSpecialTasks)
            {
                return "1";
            }

            var discoverFlag = await ResolveDiscoverLookupFlagAsync(device, cancellationToken);
            if (discoverFlag == "SDFT")
            {
                return "SDFT";
            }

            if (clientUpdateStatus == ClientUpdateStatus.NoChange)
            {
                device.IpAddress = request.IpAddress.Split(',')[0].Trim();
            }

            return "0";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve autoDiscoverFlag for {MacAddress}", device.MacAddress);
            return "NOK";
        }
    }

    private async Task<string> ResolveDiscoverLookupFlagAsync(
        Device device,
        CancellationToken cancellationToken)
    {
        if (await RequiresInventoryDiscoveryAsync(device, cancellationToken))
        {
            return "SDFT";
        }

        return "exists";
    }

    private async Task<bool> RequiresInventoryDiscoveryAsync(
        Device device,
        CancellationToken cancellationToken)
    {
        if (!device.IsRegistered || device.EnrollmentState != EnrollmentState.Active)
        {
            return true;
        }

        return device.Inventory is null &&
               !await _dbContext.DeviceInventories.AnyAsync(i => i.DeviceId == device.Id, cancellationToken);
    }

    private async Task ProcessAgentAcknowledgementsAsync(
        Device device,
        AgentClientStatusRequest request,
        string clientStatus,
        CancellationToken cancellationToken,
        bool requireSingleIp = true)
    {
        if (requireSingleIp && request.IpAddress.Split(',').Length != 1)
        {
            return;
        }

        if (clientStatus == ClientPowerStatus.On)
        {
            var wakeTask = device.Tasks.FirstOrDefault(t =>
                t.ModuleName == "Wake On Lan" &&
                t.Status is DeviceTaskStatus.Pending or DeviceTaskStatus.InProcess);

            if (wakeTask is not null)
            {
                DeviceTaskOperations.SetCompletion(wakeTask, DeviceTaskStatus.Completed);
                DeviceTaskOperations.ApplyDeviceStateAfterCompletion(device, wakeTask, DeviceTaskStatus.Completed, clientStatus);
            }
        }

        if (request.ShutdownAck == ShutdownAcknowledgement.Shutdown)
        {
            var shutdownTask = device.Tasks.FirstOrDefault(t =>
                t.FunctionName == "Shutdown" &&
                (request.TaskId == 0 || t.LegacyTaskId == request.TaskId));

            if (shutdownTask is not null)
            {
                DeviceTaskOperations.SetCompletion(shutdownTask, DeviceTaskStatus.Completed);
                DeviceTaskOperations.ApplyDeviceStateAfterCompletion(device, shutdownTask, DeviceTaskStatus.Completed, clientStatus);
            }

            device.IsOnline = false;
            device.ClientStatus = ClientPowerStatus.Off;
        }
        else if (request.ShutdownAck == ShutdownAcknowledgement.Restart)
        {
            var restartTask = device.Tasks.FirstOrDefault(t =>
                t.FunctionName == "Restart" &&
                (request.TaskId == 0 || t.LegacyTaskId == request.TaskId));

            if (restartTask is not null)
            {
                DeviceTaskOperations.SetCompletion(restartTask, DeviceTaskStatus.Completed);
                DeviceTaskOperations.ApplyDeviceStateAfterCompletion(device, restartTask, DeviceTaskStatus.Completed, clientStatus);
            }

            device.IsOnline = clientStatus == ClientPowerStatus.On;
            device.ClientStatus = clientStatus;
        }
    }

    private static string NormalizeWins(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase))
        {
            return "...";
        }

        return value;
    }

    private static string NormalizeLoginUserName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.Contains('$'))
        {
            return value.Split('$')[0].Trim();
        }

        if (value.Contains("//"))
        {
            return value.Split("//", StringSplitOptions.None)[0].Trim();
        }

        return value.Trim();
    }
}
