using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services;

/// <summary>
/// PR3: expands Screen Saver compact task references at agent poll time (ADR-0005 Option B).
/// Wraps <see cref="AgentTaskService"/> without modifying its constructor surface.
/// </summary>
public sealed class ScreenSaverHydratingAgentTaskService : IAgentTaskService
{
    private readonly AgentTaskService _inner;
    private readonly IWindowsScreenSaverTaskPayloadHydrator _screenSaverHydrator;
    private readonly ILogger<ScreenSaverHydratingAgentTaskService> _logger;

    public ScreenSaverHydratingAgentTaskService(
        AgentTaskService inner,
        IWindowsScreenSaverTaskPayloadHydrator screenSaverHydrator,
        ILogger<ScreenSaverHydratingAgentTaskService> logger)
    {
        _inner = inner;
        _screenSaverHydrator = screenSaverHydrator;
        _logger = logger;
    }

    public async Task<AgentPendingTasksResponse> GetPendingTasksAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var response = await _inner.GetPendingTasksAsync(deviceId, cancellationToken);

        foreach (var task in response.Tasks)
        {
            if (!_screenSaverHydrator.CanHydrate(task.ModuleName))
            {
                continue;
            }

            var hydrated = await _screenSaverHydrator.HydrateFunctionParameterAsync(
                task.FunctionParameter,
                deviceId,
                task.LegacyTaskId,
                cancellationToken);

            if (hydrated is not null)
            {
                task.FunctionParameter = hydrated;
                continue;
            }

            _logger.LogWarning(
                "Screen Saver task {TaskId} hydration failed for device {DeviceId}; returning compact functionParameter",
                task.Id,
                deviceId);
        }

        return response;
    }

    public Task AcknowledgeTasksAsync(
        Guid deviceId,
        AgentTaskAckBatchRequest request,
        CancellationToken cancellationToken = default) =>
        _inner.AcknowledgeTasksAsync(deviceId, request, cancellationToken);

    public Task<AdminQueueTaskResult> QueueTaskForDeviceAsync(
        Guid tenantId,
        string macAddress,
        AdminQueueTaskRequest request,
        CancellationToken cancellationToken = default) =>
        _inner.QueueTaskForDeviceAsync(tenantId, macAddress, request, cancellationToken);
}
