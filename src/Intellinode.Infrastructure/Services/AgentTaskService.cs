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
    private readonly ILogger<AgentTaskService> _logger;

    public AgentTaskService(IntellinodeDbContext dbContext, ILogger<AgentTaskService> logger)
    {
        _dbContext = dbContext;
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

        return new AgentPendingTasksResponse
        {
            Tasks = tasks.Select(MapPendingTask).ToList()
        };
    }

    public async Task AcknowledgeTasksAsync(
        Guid deviceId,
        AgentTaskAckBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var device = await _dbContext.Devices
            .Include(d => d.Tasks)
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
            DeviceTaskOperations.ApplyDeviceStateAfterCompletion(device, task, status);

            if (!string.IsNullOrWhiteSpace(ack.AckCode))
            {
                _logger.LogInformation(
                    "Task {TaskId} (legacy {LegacyTaskId}) acknowledged with code {AckCode} as {Status} for device {DeviceId}",
                    task.Id,
                    task.LegacyTaskId,
                    ack.AckCode,
                    ack.Status,
                    deviceId);
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

    /// <summary>
    /// Assigns the next monotonic legacy task id per device (max existing + 1).
    /// </summary>
    private async Task<int> GetNextLegacyTaskIdAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var maxLegacyId = await _dbContext.DeviceTasks
            .Where(t => t.DeviceId == deviceId)
            .MaxAsync(t => (int?)t.LegacyTaskId, cancellationToken);

        return (maxLegacyId ?? 0) + 1;
    }

    private static AgentPendingTaskDto MapPendingTask(DeviceTask task) =>
        new()
        {
            Id = task.Id,
            LegacyTaskId = task.LegacyTaskId,
            ModuleName = task.ModuleName,
            FunctionName = task.FunctionName,
            FunctionParameter = task.FunctionParameter,
            Signal = DeviceTaskOperations.ExtractSignal(task.ExtraData),
            Status = DeviceTaskOperations.MapStatusToString(task.Status)
        };
}
