using System.Text.Json;

namespace Intellinode.Application.Contracts.Agents;

public static class AgentApiPaths
{
    public const string Bootstrap = "/api/v1/agents/bootstrap";
    public const string AuthToken = "/api/v1/agents/auth/token";
    public const string AuthRefresh = "/api/v1/agents/auth/refresh";
    public const string AuthRevoke = "/api/v1/agents/auth/revoke";
    public const string Heartbeat = "/api/v1/agents/heartbeat";
    public const string Inventory = "/api/v1/agents/inventory";
    public const string WindowsRegister = "/api/v1/agents/windows/register";
    public const string WindowsEnroll = "/api/v1/agents/windows/enroll";
    public const string TasksPending = "/api/v1/agents/tasks/pending";
    public const string TasksAck = "/api/v1/agents/tasks/ack";
    public const string Config = "/api/v1/agents/config";
    public const string ConfigAck = "/api/v1/agents/config/ack";
    public const string WindowsTaskbarLive = "/api/v1/agents/windows/taskbar/live";
}

public sealed class AgentBootstrapResponse
{
    public string ServerBaseUrl { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = string.Empty;
    public int DefaultPollIntervalSeconds { get; set; }
    public AgentEndpointPaths Endpoints { get; set; } = new();
}

public sealed class AgentEndpointPaths
{
    public string Bootstrap { get; set; } = AgentApiPaths.Bootstrap;
    public string AuthToken { get; set; } = AgentApiPaths.AuthToken;
    public string AuthRefresh { get; set; } = AgentApiPaths.AuthRefresh;
    public string AuthRevoke { get; set; } = AgentApiPaths.AuthRevoke;
    public string Heartbeat { get; set; } = AgentApiPaths.Heartbeat;
    public string Inventory { get; set; } = AgentApiPaths.Inventory;
    public string WindowsRegister { get; set; } = AgentApiPaths.WindowsRegister;
    public string WindowsEnroll { get; set; } = AgentApiPaths.WindowsEnroll;
    public string TasksPending { get; set; } = AgentApiPaths.TasksPending;
    public string TasksAck { get; set; } = AgentApiPaths.TasksAck;
    public string Config { get; set; } = AgentApiPaths.Config;
    public string ConfigAck { get; set; } = AgentApiPaths.ConfigAck;
    public string WindowsTaskbarLive { get; set; } = AgentApiPaths.WindowsTaskbarLive;
}

public sealed class WindowsAgentEnrollRequest
{
    public string Token { get; set; } = string.Empty;
    public string? DeviceIdentity { get; set; }
}

public sealed class WindowsAgentInventoryRequest
{
    public JsonElement? Hardware { get; set; }
    public JsonElement? Network { get; set; }
    public JsonElement? OsInfo { get; set; }
    public JsonElement? Security { get; set; }

    public AgentInventoryRequest ToAgentInventoryRequest() => new()
    {
        Hardware = Hardware,
        Network = Network,
        OsInfo = OsInfo,
        Security = Security
    };
}

public sealed class WindowsAgentRegisterRequest
{
    public string Token { get; set; } = string.Empty;
    public string? DeviceIdentity { get; set; }
    public WindowsAgentInventoryRequest Inventory { get; set; } = new();
}

public sealed class AgentErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class AgentEnrollResult
{
    public AgentAuthResponse? AuthResponse { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => AuthResponse is not null;

    public static AgentEnrollResult Success(AgentAuthResponse response) =>
        new() { AuthResponse = response };

    public static AgentEnrollResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class AgentInventoryRequest
{
    public JsonElement? Hardware { get; set; }
    public JsonElement? Network { get; set; }
    public JsonElement? OsInfo { get; set; }
    public JsonElement? Security { get; set; }
}

public sealed class AdminEnrollmentLinkResponse
{
    public string EnrollmentUrl { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresUtc { get; set; }
}

public sealed class AgentClientStatusRequest{
    public string ClientStatus { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public bool Dhcp { get; set; }
    public string HostName { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string CommunicationIpAddress { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string PrimaryDns { get; set; } = string.Empty;
    public string SecondaryDns { get; set; } = string.Empty;
    public string PrimaryWins { get; set; } = string.Empty;
    public string SecondaryWins { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string ShutdownAck { get; set; } = string.Empty;
    public string LoginUserName { get; set; } = string.Empty;
    public int PollInterval { get; set; }
    public string CommunicationType { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Workgroup { get; set; } = string.Empty;
    public bool IsDomainJoined { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string License { get; set; } = string.Empty;
    public string AgentUpTime { get; set; } = string.Empty;
    public int TaskId { get; set; }
    public int AgentAction { get; set; }
}

public sealed class HeartbeatResponse
{
    public string AutoDiscoverFlag { get; set; } = "exists";
    public string? ClientUpdateStatus { get; set; }
    public string? HostName { get; set; }
    public string? IpAddress { get; set; }
    public DateTime LastHeartbeatUtc { get; set; }
    public bool ConfigPending { get; set; }
}

public sealed class AgentAuthRequest
{
    public string DeviceIdentity { get; set; } = string.Empty;
    public int IsRegistered { get; set; } = 1;
}

public sealed class AgentRefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class AgentRevokeRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class AgentRefreshResult
{
    public AgentAuthResponse? AuthResponse { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => AuthResponse is not null;

    public static AgentRefreshResult Success(AgentAuthResponse response) =>
        new() { AuthResponse = response };

    public static AgentRefreshResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class AgentAuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresUtc { get; set; }
    public string DeviceIdentity { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
    public string? Error { get; set; }
    public string ServerBaseUrl { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; }
}
public sealed class AdminLoginRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class AdminLoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresUtc { get; set; }
    public string UserName { get; set; } = string.Empty;
}

public sealed class AgentPendingTaskDto
{
    public Guid Id { get; set; }
    public int LegacyTaskId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string FunctionParameter { get; set; } = string.Empty;
    public string Signal { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class AgentPendingTasksResponse
{
    public List<AgentPendingTaskDto> Tasks { get; set; } = [];
}

public sealed class AgentTaskAckRequest
{
    public int? LegacyTaskId { get; set; }
    public Guid? TaskId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AckCode { get; set; }

    /// <summary>FusionX Task_Schedule_Logs.Reason when status is Failed.</summary>
    public string? Reason { get; set; }
}

public sealed class AgentTaskAckBatchRequest
{
    public List<AgentTaskAckRequest> Acknowledgements { get; set; } = [];
}

public sealed class AdminQueueTaskRequest
{
    public string ModuleName { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string? FunctionParameter { get; set; }
    public int LegacyTaskId { get; set; }
    public string? Signal { get; set; }
    public string? ExtraData { get; set; }
}

public sealed class AdminQueueTaskResponse
{
    public Guid TaskId { get; set; }
    public int LegacyTaskId { get; set; }
}

public sealed class AdminQueueTaskResult
{
    public AdminQueueTaskResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static AdminQueueTaskResult Success(AdminQueueTaskResponse response) =>
        new() { Response = response };

    public static AdminQueueTaskResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class AgentInventorySubmitResponse
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }

    public bool IsSuccess => string.IsNullOrEmpty(ErrorCode);
}
