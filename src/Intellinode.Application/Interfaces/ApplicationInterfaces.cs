using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Contracts.Agents;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;

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

public interface IDiscoverLookupWriter
{
    Task UpsertPendingFromInventoryAsync(
        Device device,
        AgentInventoryRequest request,
        CancellationToken cancellationToken = default);

    Task SyncPendingFromHeartbeatAsync(
        Device device,
        AgentClientStatusRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAgentCommunicationLogWriter
{
    Task LogAsync(
        Guid? deviceId,
        string? macAddress,
        string direction,
        string endpoint,
        string? commandCode,
        string? payloadSummary,
        CancellationToken cancellationToken = default);
}

public interface IExceptionLogWriter
{
    Task LogAsync(
        string source,
        Exception exception,
        Guid? deviceId = null,
        Guid? adminId = null,
        string? requestPath = null,
        string? httpMethod = null,
        CancellationToken cancellationToken = default);
}

public interface IDeviceManagerService
{
    Task<DeviceTreeResponse> GetTreeAsync(DeviceTreeQuery query, CancellationToken cancellationToken = default);
    Task<DeviceManagerGroupInfoDto?> GetGroupInfoAsync(Guid groupId, CancellationToken cancellationToken = default);
    Task<DeviceManagerDeviceInfoDto?> GetDeviceInfoAsync(Guid deviceId, CancellationToken cancellationToken = default);
}

public interface IDiscoverLookupService
{
    Task<PagedDiscoverLookupResponse> ListAsync(DiscoverLookupQuery query, CancellationToken cancellationToken = default);
    Task<DiscoverLookupDetailDto?> GetByMacAsync(string macAddress, CancellationToken cancellationToken = default);
    Task<DiscoverLookupStatsResponse> GetStatsAsync(CancellationToken cancellationToken = default);
    Task<DiscoverLookupOperationResult<ApproveDiscoveryResponse>> ApproveAsync(
        string macAddress,
        Guid adminId,
        ApproveDiscoveryRequest request,
        CancellationToken cancellationToken = default);
    Task<DiscoverLookupOperationResult<bool>> RejectAsync(
        string macAddress,
        Guid adminId,
        RejectDiscoveryRequest request,
        CancellationToken cancellationToken = default);
    Task<BulkApproveDiscoveryResponse> BulkApproveAsync(
        Guid adminId,
        BulkApproveDiscoveryRequest request,
        CancellationToken cancellationToken = default);

    Task<DiscoverLookupOperationResult<bool>> DismissAsync(
        string macAddress,
        Guid adminId,
        DismissDiscoveryRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAgentInventoryService
{
    Task<AgentInventorySubmitResponse> UpsertInventoryAsync(
        Guid deviceId,
        AgentInventoryRequest request,
        InventorySubmissionKind kind = InventorySubmissionKind.SelfDiscovery,
        CancellationToken cancellationToken = default);

    Task ApplyInventoryAsync(
        Guid deviceId,
        AgentInventoryRequest request,
        InventorySubmissionKind kind = InventorySubmissionKind.TokenEnrollment,
        CancellationToken cancellationToken = default);
}

public interface IAgentTaskService
{
    Task<AgentPendingTasksResponse> GetPendingTasksAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task AcknowledgeTasksAsync(Guid deviceId, AgentTaskAckBatchRequest request, CancellationToken cancellationToken = default);
    Task<AdminQueueTaskResult> QueueTaskForDeviceAsync(Guid tenantId, string macAddress, AdminQueueTaskRequest request, CancellationToken cancellationToken = default);
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
