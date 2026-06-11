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

public interface ISystemSettingService
{
    Task<SystemSettingExecuteNowResult> ExecuteNowAsync(
        SystemSettingExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<SystemSettingBulkResult> ExecuteNowBulkAsync(
        SystemSettingExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<SystemSettingQueueResult> QueueAsync(
        SystemSettingQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<SystemSettingQueueResult> TemplateQueueAsync(
        SystemSettingTemplateQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<SystemSettingCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default);

    Task<SystemSettingHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        SystemSettingHistoryQuery query,
        CancellationToken cancellationToken = default);
}

public interface IKeyboardSettingsService
{
    Task<KeyboardExecuteNowResult> ExecuteNowAsync(
        KeyboardExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<KeyboardQueueResult> QueueAsync(
        KeyboardQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<KeyboardCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default);

    Task<KeyboardHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        KeyboardHistoryQuery query,
        CancellationToken cancellationToken = default);
}

public interface IWindows8021xSettingsService
{
    Task<Windows8021xExecuteNowResult> ExecuteNowAsync(
        Windows8021xExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<Windows8021xQueueResult> QueueAsync(
        Windows8021xQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<Windows8021xCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default);

    Task<Windows8021xHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        Windows8021xHistoryQuery query,
        CancellationToken cancellationToken = default);
}

public interface IMouseSettingsService
{
    Task<MouseExecuteNowResult> ExecuteNowAsync(
        MouseExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<MouseQueueResult> QueueAsync(
        MouseQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<MouseCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default);

    Task<MouseHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        MouseHistoryQuery query,
        CancellationToken cancellationToken = default);
}

public interface IDisplaySettingsService
{
    Task<DisplayExecuteNowResult> ExecuteNowAsync(
        DisplayExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<DisplayQueueResult> QueueAsync(
        DisplayQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<DisplayCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default);

    Task<DisplayHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        DisplayHistoryQuery query,
        CancellationToken cancellationToken = default);
}

public interface IEffectiveAgentSettingsResolver
{
    Task<EffectiveAgentSettings> ResolveEffectiveGeneralAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<AgentAdvancedConfigDto> ResolveEffectiveAdvancedAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<EffectiveDeviceSettingsDto?> ResolveEffectiveCombinedByMacAsync(string macAddress, CancellationToken cancellationToken = default);
    Task<AgentConfigAckResponse> AcknowledgeConfigAsync(Guid deviceId, AgentConfigAckRequest request, CancellationToken cancellationToken = default);
    Task<bool> HasPendingConfigAsync(Guid deviceId, CancellationToken cancellationToken = default);
}

public interface IWindows8021xPayloadBuilder
{
    string BuildAgentPayload(string settingsJson);
    string BuildCompactTaskReference(long settingsVersion);
    bool TryParseCompactTaskReference(string functionParameter, out long settingsVersion);
}

public interface IWindows8021xTaskPayloadHydrator
{
    bool CanHydrate(string moduleName);
    Task<string?> HydrateFunctionParameterAsync(
        string storedFunctionParameter,
        Guid deviceId,
        CancellationToken cancellationToken = default);
}

public interface IWindowsWirelessPropertiesPayloadBuilder
{
    string BuildAgentPayload(string settingsJson);
    string BuildCompactTaskReference(long settingsVersion, long profileKey);
    bool TryParseCompactTaskReference(string functionParameter, out long settingsVersion, out long profileKey);
    string BuildInnerSettingsJson(WindowsWirelessPropertiesProfileRequest profile, WirelessProfileOperation operation);
}

public interface IWindowsWirelessPropertiesTaskPayloadHydrator
{
    bool CanHydrate(string moduleName);
    Task<string?> HydrateFunctionParameterAsync(
        string storedFunctionParameter,
        Guid deviceId,
        CancellationToken cancellationToken = default);
}

public interface IWindowsWirelessPropertiesSettingsService
{
    Task<WindowsWirelessPropertiesExecuteNowResult> ExecuteNowAsync(
        WindowsWirelessPropertiesExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<WindowsWirelessPropertiesQueueResult> QueueAsync(
        WindowsWirelessPropertiesQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<WindowsWirelessPropertiesDeleteExecuteNowResult> DeleteExecuteNowAsync(
        WindowsWirelessPropertiesDeleteRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<WindowsWirelessPropertiesDeleteQueueResult> DeleteQueueAsync(
        WindowsWirelessPropertiesDeleteRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<WindowsWirelessPropertiesListResult> ListProfilesAsync(
        string macAddress,
        CancellationToken cancellationToken = default);

    Task<WindowsWirelessPropertiesProfileResult> GetProfileAsync(
        string macAddress,
        string ssid,
        CancellationToken cancellationToken = default);

    Task<WindowsWirelessPropertiesHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        WindowsWirelessPropertiesHistoryQuery query,
        CancellationToken cancellationToken = default);

    Task<WindowsWirelessPropertiesBulkResult> ExecuteNowBulkAsync(
        WindowsWirelessPropertiesExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<WindowsWirelessPropertiesBulkResult> ExecuteNowGroupAsync(
        Guid groupId,
        WindowsWirelessPropertiesExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<WindowsWirelessPropertiesBulkResult> DeleteExecuteNowBulkAsync(
        WindowsWirelessPropertiesDeleteExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<WindowsWirelessPropertiesBulkResult> DeleteExecuteNowGroupAsync(
        Guid groupId,
        WindowsWirelessPropertiesDeleteExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);
}

public interface IWindowsComputerNamePayloadBuilder
{
    string BuildHostRenamePayload(WindowsComputerNameHostRenamePayloadRequest request);
    string BuildDomainJoinPayload(WindowsComputerNameDomainJoinPayloadRequest request);
    string GetModuleNameForApplyMode(ComputerNameApplyMode mode);
}

public interface IWindowsComputerNameSettingsService
{
    Task<WindowsComputerNameExecuteNowResult> ExecuteNowAsync(
        WindowsComputerNameExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<WindowsComputerNameQueueResult> QueueAsync(
        WindowsComputerNameQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<WindowsComputerNameCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default);

    Task<WindowsComputerNameHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        WindowsComputerNameHistoryQuery query,
        CancellationToken cancellationToken = default);

    Task<WindowsComputerNameBulkResult> ExecuteNowBulkAsync(
        WindowsComputerNameExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<WindowsComputerNameBulkResult> ExecuteNowGroupAsync(
        Guid groupId,
        WindowsComputerNameExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);
}

public interface IWindowsEthernetSetupSettingsService
{
    Task<WindowsEthernetSetupCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default);

    Task<WindowsEthernetSetupExecuteNowResult> ExecuteNowAsync(
        WindowsEthernetSetupExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<WindowsEthernetSetupQueueResult> QueueAsync(
        WindowsEthernetSetupQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<WindowsEthernetSetupHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        WindowsEthernetSetupHistoryQuery query,
        CancellationToken cancellationToken = default);

    Task<WindowsEthernetSetupBulkResult> ExecuteNowBulkAsync(
        WindowsEthernetSetupExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);

    Task<WindowsEthernetSetupBulkResult> ExecuteNowGroupAsync(
        Guid groupId,
        WindowsEthernetSetupExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default);
}

public interface IWindowsEthernetSetupPayloadBuilder
{
    string BuildEthernetPayload(WindowsEthernetSetupPayloadRequest request);
}

public interface IIntellinodeDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
