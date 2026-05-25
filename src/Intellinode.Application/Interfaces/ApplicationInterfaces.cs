using Intellinode.Application.Contracts.Agents;

namespace Intellinode.Application.Interfaces;

public interface IHeartbeatService
{
    Task<HeartbeatResponse> ProcessHeartbeatAsync(AgentClientStatusRequest request, CancellationToken cancellationToken = default);
}

public interface IAgentAuthService
{
    Task<AgentAuthResponse> AuthenticateAsync(AgentAuthRequest request, CancellationToken cancellationToken = default);
    Task<AgentRefreshResult> RefreshAsync(AgentRefreshRequest request, CancellationToken cancellationToken = default);
    Task RevokeRefreshTokenAsync(AgentRevokeRequest request, CancellationToken cancellationToken = default);
}

public interface IAdminAuthService
{
    Task<AdminLoginResponse?> LoginAsync(AdminLoginRequest request, CancellationToken cancellationToken = default);
}

public interface ITokenService
{
    string CreateAgentAccessToken(Guid deviceId, string macAddress);
    string CreateAdminAccessToken(Guid adminId, string userName);
    string CreateRefreshToken();
    string HashToken(string token);
}

public interface IAgentServerUrlProvider
{
    string ServerBaseUrl { get; }
    string ApiBaseUrl { get; }
    int DefaultPollIntervalSeconds { get; }
    AgentBootstrapResponse CreateBootstrapResponse();
    void ApplyProvisioningUrls(AgentAuthResponse response);
    string BuildEnrollmentUrl(string token);
}

public interface IAgentBootstrapService
{
    AgentBootstrapResponse GetBootstrap();
}

public interface IAgentEnrollmentService
{
    Task<AdminEnrollmentLinkResponse> CreateEnrollmentLinkAsync(
        Guid adminId,
        string? macAddress,
        CancellationToken cancellationToken = default);

    Task<AgentEnrollResult> EnrollAsync(
        AgentEnrollRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAgentInventoryService
{
    Task UpsertInventoryAsync(
        Guid deviceId,
        AgentInventoryRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAgentTaskService
{
    Task<AgentPendingTasksResponse> GetPendingTasksAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task AcknowledgeTasksAsync(Guid deviceId, AgentTaskAckBatchRequest request, CancellationToken cancellationToken = default);
    Task<AdminQueueTaskResponse?> QueueTaskForDeviceAsync(Guid tenantId, string macAddress, AdminQueueTaskRequest request, CancellationToken cancellationToken = default);
}

public interface IIntellinodeDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
