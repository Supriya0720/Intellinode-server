using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services;

/// <summary>
/// PR3: expands User Interface compact task references at agent poll time.
/// </summary>
public sealed class UserInterfaceHydratingAgentTaskService : IAgentTaskService
{
    private readonly ScreenSaverHydratingAgentTaskService _inner;
    private readonly IWindowsUserInterfaceTaskPayloadHydrator _userInterfaceHydrator;
    private readonly ILogger<UserInterfaceHydratingAgentTaskService> _logger;

    public UserInterfaceHydratingAgentTaskService(
        ScreenSaverHydratingAgentTaskService inner,
        IWindowsUserInterfaceTaskPayloadHydrator userInterfaceHydrator,
        ILogger<UserInterfaceHydratingAgentTaskService> logger)
    {
        _inner = inner;
        _userInterfaceHydrator = userInterfaceHydrator;
        _logger = logger;
    }

    public async Task<AgentPendingTasksResponse> GetPendingTasksAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var response = await _inner.GetPendingTasksAsync(deviceId, cancellationToken);

        foreach (var task in response.Tasks)
        {
            if (!_userInterfaceHydrator.CanHydrate(task.ModuleName))
            {
                continue;
            }

            var hydrated = await _userInterfaceHydrator.HydrateFunctionParameterAsync(
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
                "User interface task {TaskId} hydration failed for device {DeviceId}; returning compact functionParameter",
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
