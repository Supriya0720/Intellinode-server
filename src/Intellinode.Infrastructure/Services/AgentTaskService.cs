using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services;

public sealed class AgentTaskService : IAgentTaskService
{
    /// <summary>
    /// FusionX-like behavior: first Pending task becomes InProcess when the agent polls pending work.
    /// </summary>
    private const bool MarkFirstPendingAsInProcess = true;

    private readonly IntellinodeDbContext _dbContext;
    private readonly KeyboardTaskAckHandler _keyboardTaskAckHandler;
    private readonly MouseTaskAckHandler _mouseTaskAckHandler;
    private readonly DisplayTaskAckHandler _displayTaskAckHandler;
    private readonly Windows8021xTaskAckHandler _windows8021xTaskAckHandler;
    private readonly WindowsComputerNameTaskAckHandler _windowsComputerNameTaskAckHandler;
    private readonly WindowsDateTimeTaskAckHandler _windowsDateTimeTaskAckHandler;
    private readonly WindowsRegionLocationTaskAckHandler _windowsRegionLocationTaskAckHandler;
    private readonly WindowsRegionalFormatTaskAckHandler _windowsRegionalFormatTaskAckHandler;
    private readonly WindowsEthernetSetupTaskAckHandler _windowsEthernetSetupTaskAckHandler;
    private readonly WindowsWirelessSetupTaskAckHandler _windowsWirelessSetupTaskAckHandler;
    private readonly WindowsWirelessPropertiesTaskAckHandler _windowsWirelessPropertiesTaskAckHandler;
    private readonly WindowsPowerManagementTaskAckHandler _windowsPowerManagementTaskAckHandler;
    private readonly IWindows8021xTaskPayloadHydrator _windows8021xHydrator;
    private readonly IWindowsWirelessPropertiesTaskPayloadHydrator _windowsWirelessPropertiesHydrator;
    private readonly IWindowsPowerManagementTaskPayloadHydrator _windowsPowerManagementHydrator;
    private readonly ILogger<AgentTaskService> _logger;

    public AgentTaskService(
        IntellinodeDbContext dbContext,
        KeyboardTaskAckHandler keyboardTaskAckHandler,
        MouseTaskAckHandler mouseTaskAckHandler,
        DisplayTaskAckHandler displayTaskAckHandler,
        Windows8021xTaskAckHandler windows8021xTaskAckHandler,
        WindowsComputerNameTaskAckHandler windowsComputerNameTaskAckHandler,
        WindowsDateTimeTaskAckHandler windowsDateTimeTaskAckHandler,
        WindowsRegionLocationTaskAckHandler windowsRegionLocationTaskAckHandler,
        WindowsRegionalFormatTaskAckHandler windowsRegionalFormatTaskAckHandler,
        WindowsEthernetSetupTaskAckHandler windowsEthernetSetupTaskAckHandler,
        WindowsWirelessSetupTaskAckHandler windowsWirelessSetupTaskAckHandler,
        WindowsWirelessPropertiesTaskAckHandler windowsWirelessPropertiesTaskAckHandler,
        WindowsPowerManagementTaskAckHandler windowsPowerManagementTaskAckHandler,
        IWindows8021xTaskPayloadHydrator windows8021xHydrator,
        IWindowsWirelessPropertiesTaskPayloadHydrator windowsWirelessPropertiesHydrator,
        IWindowsPowerManagementTaskPayloadHydrator windowsPowerManagementHydrator,
        ILogger<AgentTaskService> logger)
    {
        _dbContext = dbContext;
        _keyboardTaskAckHandler = keyboardTaskAckHandler;
        _mouseTaskAckHandler = mouseTaskAckHandler;
        _displayTaskAckHandler = displayTaskAckHandler;
        _windows8021xTaskAckHandler = windows8021xTaskAckHandler;
        _windowsComputerNameTaskAckHandler = windowsComputerNameTaskAckHandler;
        _windowsDateTimeTaskAckHandler = windowsDateTimeTaskAckHandler;
        _windowsRegionLocationTaskAckHandler = windowsRegionLocationTaskAckHandler;
        _windowsRegionalFormatTaskAckHandler = windowsRegionalFormatTaskAckHandler;
        _windowsEthernetSetupTaskAckHandler = windowsEthernetSetupTaskAckHandler;
        _windowsWirelessSetupTaskAckHandler = windowsWirelessSetupTaskAckHandler;
        _windowsWirelessPropertiesTaskAckHandler = windowsWirelessPropertiesTaskAckHandler;
        _windowsPowerManagementTaskAckHandler = windowsPowerManagementTaskAckHandler;
        _windows8021xHydrator = windows8021xHydrator;
        _windowsWirelessPropertiesHydrator = windowsWirelessPropertiesHydrator;
        _windowsPowerManagementHydrator = windowsPowerManagementHydrator;
        _logger = logger;
    }

    public async Task<AgentPendingTasksResponse> GetPendingTasksAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var tasks = await _dbContext.DeviceTasks
            .Where(t => t.DeviceId == deviceId &&
                        (t.Status == DeviceTaskStatus.Pending || t.Status == DeviceTaskStatus.InProcess))
            .OrderBy(t => t.CreatedUtc)
            .ToListAsync(cancellationToken);

        if (MarkFirstPendingAsInProcess)
        {
            var firstPending = tasks.FirstOrDefault(t => t.Status == DeviceTaskStatus.Pending);
            if (firstPending is not null)
            {
                firstPending.Status = DeviceTaskStatus.InProcess;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        var mappedTasks = new List<AgentPendingTaskDto>(tasks.Count);
        foreach (var task in tasks)
        {
            var functionParameter = task.FunctionParameter;
            if (_windows8021xHydrator.CanHydrate(task.ModuleName))
            {
                var hydrated = await _windows8021xHydrator.HydrateFunctionParameterAsync(
                    task.FunctionParameter,
                    deviceId,
                    cancellationToken);
                if (hydrated is not null)
                {
                    functionParameter = hydrated;
                }
                else
                {
                    _logger.LogWarning(
                        "Windows_802_1x task {TaskId} hydration failed for device {DeviceId}; returning compact functionParameter",
                        task.Id,
                        deviceId);
                }
            }

            if (_windowsWirelessPropertiesHydrator.CanHydrate(task.ModuleName))
            {
                var hydrated = await _windowsWirelessPropertiesHydrator.HydrateFunctionParameterAsync(
                    task.FunctionParameter,
                    deviceId,
                    cancellationToken);
                if (hydrated is not null)
                {
                    functionParameter = hydrated;
                }
                else
                {
                    _logger.LogWarning(
                        "Wireless Network Security task {TaskId} hydration failed for device {DeviceId}; returning compact functionParameter",
                        task.Id,
                        deviceId);
                }
            }

            if (_windowsPowerManagementHydrator.CanHydrate(task.ModuleName))
            {
                var hydrated = await _windowsPowerManagementHydrator.HydrateFunctionParameterAsync(
                    task.FunctionParameter,
                    deviceId,
                    task.LegacyTaskId,
                    cancellationToken);
                if (hydrated is not null)
                {
                    functionParameter = hydrated;
                }
                else
                {
                    _logger.LogWarning(
                        "Power Management Settings task {TaskId} hydration failed for device {DeviceId}; returning compact functionParameter",
                        task.Id,
                        deviceId);
                }
            }

            mappedTasks.Add(MapPendingTask(task, functionParameter));
        }

        return new AgentPendingTasksResponse
        {
            Tasks = mappedTasks
        };
    }

    public async Task AcknowledgeTasksAsync(
        Guid deviceId,
        AgentTaskAckBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var device = await _dbContext.Devices
            .Include(d => d.Tasks)
            .Include(d => d.KeyboardSettings)
            .Include(d => d.MouseSettings)
            .Include(d => d.DisplaySettings)
            .Include(d => d.Windows8021xSettings)
            .Include(d => d.WindowsComputerNameSettings)
            .Include(d => d.WindowsDateTimeSettings)
            .Include(d => d.WindowsRegionLocationSettings)
            .Include(d => d.WindowsRegionalFormatSettings)
            .Include(d => d.WindowsWirelessProfiles)
            .Include(d => d.WindowsPowerManagementSettings)
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);

        if (device is null)
        {
            throw new InvalidOperationException($"Device '{deviceId}' was not found.");
        }

        foreach (var ack in request.Acknowledgements)
        {
            var task = ResolveTask(device, ack);
            if (task is null)
            {
                throw new InvalidOperationException(
                    $"Task not found for device '{deviceId}' (taskId={ack.TaskId}, legacyTaskId={ack.LegacyTaskId}).");
            }

            var status = DeviceTaskOperations.ParseAckStatus(ack.Status);
            DeviceTaskOperations.SetCompletion(task, status);

            if (KeyboardTaskAckHandler.IsKeyboardTask(task))
            {
                await _keyboardTaskAckHandler.ApplyAckAsync(
                    device,
                    task,
                    status,
                    ack.Reason,
                    cancellationToken);
            }

            if (MouseTaskAckHandler.IsMouseTask(task))
            {
                await _mouseTaskAckHandler.ApplyAckAsync(
                    device,
                    task,
                    status,
                    ack.Reason,
                    cancellationToken);
            }

            if (DisplayTaskAckHandler.IsDisplayTask(task))
            {
                await _displayTaskAckHandler.ApplyAckAsync(
                    device,
                    task,
                    status,
                    ack.Reason,
                    cancellationToken);
            }

            if (Windows8021xTaskAckHandler.IsWindows8021xTask(task))
            {
                await _windows8021xTaskAckHandler.ApplyAckAsync(
                    device,
                    task,
                    status,
                    ack.Reason,
                    cancellationToken);
            }

            if (WindowsComputerNameTaskAckHandler.IsComputerNameTask(task))
            {
                await _windowsComputerNameTaskAckHandler.ApplyAckAsync(
                    device,
                    task,
                    status,
                    ack.Reason,
                    cancellationToken);
            }

            if (WindowsDateTimeTaskAckHandler.IsDateTimeTask(task))
            {
                await _windowsDateTimeTaskAckHandler.ApplyAckAsync(
                    device,
                    task,
                    status,
                    ack.Reason,
                    cancellationToken);
            }

            if (WindowsRegionLocationTaskAckHandler.IsRegionLocationTask(task))
            {
                await _windowsRegionLocationTaskAckHandler.ApplyAckAsync(
                    device,
                    task,
                    status,
                    ack.Reason,
                    cancellationToken);
            }

            if (WindowsRegionalFormatTaskAckHandler.IsRegionalFormatTask(task))
            {
                await _windowsRegionalFormatTaskAckHandler.ApplyAckAsync(
                    device,
                    task,
                    status,
                    ack.Reason,
                    cancellationToken);
            }

            if (WindowsEthernetSetupTaskAckHandler.IsEthernetSetupTask(task))
            {
                await _windowsEthernetSetupTaskAckHandler.ApplyAckAsync(
                    device,
                    task,
                    status,
                    ack.Reason,
                    cancellationToken);
            }

            if (WindowsWirelessSetupTaskAckHandler.IsWirelessSetupTask(task))
            {
                await _windowsWirelessSetupTaskAckHandler.ApplyAckAsync(
                    device,
                    task,
                    status,
                    ack.Reason,
                    cancellationToken);
            }

            if (WindowsWirelessPropertiesTaskAckHandler.IsWirelessPropertiesTask(task))
            {
                await _windowsWirelessPropertiesTaskAckHandler.ApplyAckAsync(
                    device,
                    task,
                    status,
                    ack.Reason,
                    cancellationToken);
            }

            if (WindowsPowerManagementTaskAckHandler.IsPowerManagementTask(task))
            {
                await _windowsPowerManagementTaskAckHandler.ApplyAckAsync(
                    device,
                    task,
                    status,
                    ack.Reason,
                    cancellationToken);
            }

            DeviceTaskOperations.ApplyDeviceStateAfterCompletion(device, task, status);

            if (!string.IsNullOrWhiteSpace(ack.AckCode) || !string.IsNullOrWhiteSpace(ack.Reason))
            {
                _logger.LogInformation(
                    "Task {TaskId} (legacy {LegacyTaskId}) acknowledged as {Status} for device {DeviceId} (ackCode={AckCode}, reason={Reason})",
                    task.Id,
                    task.LegacyTaskId,
                    ack.Status,
                    deviceId,
                    ack.AckCode ?? string.Empty,
                    ack.Reason ?? string.Empty);
            }
        }

        device.UpdatedUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdminQueueTaskResult> QueueTaskForDeviceAsync(
        Guid tenantId,
        string macAddress,
        AdminQueueTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedMac = macAddress.Trim();
        var device = await _dbContext.Devices
            .FirstOrDefaultAsync(
                d => d.TenantId == tenantId && d.MacAddress == normalizedMac,
                cancellationToken);

        if (device is null)
        {
            return AdminQueueTaskResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        if (!DeviceEnrollmentGuard.IsManaged(device.EnrollmentState))
        {
            if (device.EnrollmentState == EnrollmentState.PendingApproval)
            {
                return AdminQueueTaskResult.Failure(
                    "DevicePendingApproval",
                    "Device is awaiting administrator approval before tasks can be queued.");
            }

            return AdminQueueTaskResult.Failure(
                "DeviceNotManaged",
                $"Device enrollment state '{device.EnrollmentState}' does not allow task queueing.");
        }

        var legacyTaskId = request.LegacyTaskId > 0
            ? request.LegacyTaskId
            : await GetNextLegacyTaskIdAsync(device.Id, cancellationToken);

        var task = new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = legacyTaskId,
            ModuleName = request.ModuleName.Trim(),
            FunctionName = request.FunctionName.Trim(),
            FunctionParameter = request.FunctionParameter?.Trim() ?? string.Empty,
            ExtraData = DeviceTaskOperations.ResolveExtraData(request.Signal, request.ExtraData),
            Status = DeviceTaskStatus.Pending,
            CreatedUtc = DateTime.UtcNow
        };

        _dbContext.DeviceTasks.Add(task);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AdminQueueTaskResult.Success(new AdminQueueTaskResponse
        {
            TaskId = task.Id,
            LegacyTaskId = task.LegacyTaskId
        });
    }

    private static DeviceTask? ResolveTask(Device device, AgentTaskAckRequest ack)
    {
        if (ack.TaskId is Guid taskId && taskId != Guid.Empty)
        {
            return device.Tasks.FirstOrDefault(t => t.Id == taskId);
        }

        if (ack.LegacyTaskId is int legacyTaskId && legacyTaskId > 0)
        {
            return device.Tasks.FirstOrDefault(t => t.LegacyTaskId == legacyTaskId);
        }

        return null;
    }

    private async Task<int> GetNextLegacyTaskIdAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var maxLegacyId = await _dbContext.DeviceTasks
            .Where(t => t.DeviceId == deviceId)
            .MaxAsync(t => (int?)t.LegacyTaskId, cancellationToken);

        return (maxLegacyId ?? 0) + 1;
    }

    private static AgentPendingTaskDto MapPendingTask(DeviceTask task, string functionParameter) =>
        new()
        {
            Id = task.Id,
            LegacyTaskId = task.LegacyTaskId,
            ModuleName = task.ModuleName,
            FunctionName = task.FunctionName,
            FunctionParameter = functionParameter,
            Signal = DeviceTaskOperations.ExtractSignal(task.ExtraData),
            Status = DeviceTaskOperations.MapStatusToString(task.Status)
        };
}
