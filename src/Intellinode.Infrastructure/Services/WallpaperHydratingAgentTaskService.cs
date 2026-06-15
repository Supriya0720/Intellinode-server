using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services;

/// <summary>
/// PR3: expands Wallpaper compact task references at agent poll time (ADR-0006 Option B).
/// Wraps <see cref="ScreenSaverHydratingAgentTaskService"/> without modifying its constructor surface.
/// </summary>
public sealed class WallpaperHydratingAgentTaskService : IAgentTaskService
{
    private readonly ScreenSaverHydratingAgentTaskService _inner;
    private readonly IWindowsWallpaperTaskPayloadHydrator _wallpaperHydrator;
    private readonly ILogger<WallpaperHydratingAgentTaskService> _logger;

    public WallpaperHydratingAgentTaskService(
        ScreenSaverHydratingAgentTaskService inner,
        IWindowsWallpaperTaskPayloadHydrator wallpaperHydrator,
        ILogger<WallpaperHydratingAgentTaskService> logger)
    {
        _inner = inner;
        _wallpaperHydrator = wallpaperHydrator;
        _logger = logger;
    }

    public async Task<AgentPendingTasksResponse> GetPendingTasksAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var response = await _inner.GetPendingTasksAsync(deviceId, cancellationToken);

        foreach (var task in response.Tasks)
        {
            if (!_wallpaperHydrator.CanHydrate(task.ModuleName))
            {
                continue;
            }

            var hydrated = await _wallpaperHydrator.HydrateFunctionParameterAsync(
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
                "Wallpaper task {TaskId} hydration failed for device {DeviceId}; returning compact functionParameter",
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
