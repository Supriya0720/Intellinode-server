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
}

public interface IWindowsAgentEnrollmentService
{
    Task<AgentEnrollResult> EnrollAsync(
        WindowsAgentEnrollRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentEnrollResult> RegisterAsync(
        WindowsAgentRegisterRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAgentInventoryService
{
    Task UpsertInventoryAsync(
        Guid deviceId,
        AgentInventoryRequest request,
        CancellationToken cancellationToken = default);

    Task ApplyInventoryAsync(
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

public interface IDeviceRemoteSettingsService
{
    Task<DeviceRemoteSettingsDto?> GetByMacAsync(string macAddress, CancellationToken cancellationToken = default);
    Task<DeviceRemoteSettingsDto?> UpsertByMacAsync(string macAddress, UpsertDeviceRemoteSettingsRequest request, Guid? adminId = null, CancellationToken cancellationToken = default);
    Task<DeviceRemoteSettingsDto?> PatchInheritanceAsync(string macAddress, PatchDeviceSettingsInheritanceRequest request, CancellationToken cancellationToken = default);
    Task<EffectiveAgentSettings> ResolveEffectiveForDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<AgentConfigResponse?> GetAgentConfigAsync(string macAddress, CancellationToken cancellationToken = default);
}

public interface IDeviceAgentAdvancedSettingsService
{
    Task<DeviceAgentAdvancedSettingsDto?> GetByMacAsync(string macAddress, CancellationToken cancellationToken = default);
    Task<DeviceAgentAdvancedSettingsDto?> UpsertByMacAsync(string macAddress, UpsertDeviceAgentAdvancedSettingsRequest request, Guid? adminId = null, CancellationToken cancellationToken = default);
}

public interface IGroupRemoteSettingsService
{
    Task<GroupRemoteSettingsDto?> GetGroupRemoteSettingsAsync(Guid groupId, CancellationToken cancellationToken = default);
    Task<GroupRemoteSettingsDto?> UpsertGroupRemoteSettingsAsync(Guid groupId, UpsertGroupRemoteSettingsRequest request, Guid? adminId = null, CancellationToken cancellationToken = default);
    Task<GroupAgentAdvancedSettingsDto?> GetGroupAdvancedSettingsAsync(Guid groupId, CancellationToken cancellationToken = default);
    Task<GroupAgentAdvancedSettingsDto?> UpsertGroupAdvancedSettingsAsync(Guid groupId, UpsertGroupAgentAdvancedSettingsRequest request, Guid? adminId = null, CancellationToken cancellationToken = default);
    Task<PropagateGroupSettingsResponse?> PropagatePendingApplyAsync(Guid groupId, Guid? adminId = null, CancellationToken cancellationToken = default);
}

public interface IEffectiveAgentSettingsResolver
{
    Task<EffectiveAgentSettings> ResolveEffectiveGeneralAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<AgentAdvancedConfigDto> ResolveEffectiveAdvancedAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<EffectiveDeviceSettingsDto?> ResolveEffectiveCombinedByMacAsync(string macAddress, CancellationToken cancellationToken = default);
    Task<AgentConfigAckResponse> AcknowledgeConfigAsync(Guid deviceId, AgentConfigAckRequest request, CancellationToken cancellationToken = default);
    Task<bool> HasPendingConfigAsync(Guid deviceId, CancellationToken cancellationToken = default);
}

public interface IIntellinodeDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
