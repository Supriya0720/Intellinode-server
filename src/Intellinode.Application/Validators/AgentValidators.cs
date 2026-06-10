using FluentValidation;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Contracts.Agents;
using Intellinode.Domain.Enums;

namespace Intellinode.Application.Validators;

public sealed class AgentClientStatusRequestValidator : AbstractValidator<AgentClientStatusRequest>
{
    public AgentClientStatusRequestValidator()
    {
        RuleFor(x => x.MacAddress).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ClientStatus).NotEmpty().MaximumLength(50);
        RuleFor(x => x.IpAddress).NotEmpty().MaximumLength(512);
    }
}

public sealed class AgentAuthRequestValidator : AbstractValidator<AgentAuthRequest>
{
    public AgentAuthRequestValidator()
    {
        RuleFor(x => x.DeviceIdentity).NotEmpty().MaximumLength(300);
    }
}

public sealed class AgentRefreshRequestValidator : AbstractValidator<AgentRefreshRequest>
{
    public AgentRefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public sealed class AgentRevokeRequestValidator : AbstractValidator<AgentRevokeRequest>
{
    public AgentRevokeRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public sealed class AdminLoginRequestValidator : AbstractValidator<AdminLoginRequest>
{
    public AdminLoginRequestValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}

public sealed class WindowsAgentEnrollRequestValidator : AbstractValidator<WindowsAgentEnrollRequest>
{
    public WindowsAgentEnrollRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(512);
        RuleFor(x => x.DeviceIdentity).MaximumLength(300);
    }
}

public sealed class WindowsAgentRegisterRequestValidator : AbstractValidator<WindowsAgentRegisterRequest>
{
    public WindowsAgentRegisterRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(512);
        RuleFor(x => x.DeviceIdentity).MaximumLength(300);
        RuleFor(x => x.Inventory)
            .NotNull()
            .SetValidator(new WindowsAgentInventoryRequestValidator());
    }
}

public sealed class WindowsAgentInventoryRequestValidator : AbstractValidator<WindowsAgentInventoryRequest>
{
    public WindowsAgentInventoryRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Hardware.HasValue || x.Network.HasValue || x.OsInfo.HasValue || x.Security.HasValue)
            .WithMessage("At least one inventory section is required.");
    }
}

public sealed class AgentInventoryRequestValidator : AbstractValidator<AgentInventoryRequest>
{
    public AgentInventoryRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Hardware.HasValue || x.Network.HasValue || x.OsInfo.HasValue || x.Security.HasValue)
            .WithMessage("At least one inventory section is required.");
    }
}

public sealed class AgentTaskAckRequestValidator : AbstractValidator<AgentTaskAckRequest>
{
    public AgentTaskAckRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.TaskId is Guid taskId && taskId != Guid.Empty || x.LegacyTaskId is int id && id > 0)
            .WithMessage("Either TaskId or LegacyTaskId must be provided.");

        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => s is "Completed" or "Failed")
            .WithMessage("Status must be 'Completed' or 'Failed'.");

        RuleFor(x => x.AckCode).MaximumLength(32);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class AgentTaskAckBatchRequestValidator : AbstractValidator<AgentTaskAckBatchRequest>
{
    public AgentTaskAckBatchRequestValidator()
    {
        RuleFor(x => x.Acknowledgements)
            .NotEmpty()
            .WithMessage("At least one acknowledgement is required.");

        RuleForEach(x => x.Acknowledgements).SetValidator(new AgentTaskAckRequestValidator());
    }
}

public sealed class AdminQueueTaskRequestValidator : AbstractValidator<AdminQueueTaskRequest>
{
    public AdminQueueTaskRequestValidator()
    {
        RuleFor(x => x.ModuleName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.FunctionName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.FunctionParameter).MaximumLength(512);
        RuleFor(x => x.LegacyTaskId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Signal).MaximumLength(512);
        RuleFor(x => x.ExtraData).MaximumLength(512);
    }
}

public sealed class UpsertDeviceRemoteSettingsRequestValidator : AbstractValidator<UpsertDeviceRemoteSettingsRequest>
{
    public UpsertDeviceRemoteSettingsRequestValidator()
    {
        RuleFor(x => x.ServerHost).MaximumLength(255);
        RuleFor(x => x.ServerPort).InclusiveBetween(1, 65535);
        RuleFor(x => x.DesiredGroupName).MaximumLength(200);
        RuleFor(x => x.AgentHostName).MaximumLength(255);
        RuleFor(x => x.CommunicationType).IsInEnum();

        RuleFor(x => x.PollIntervalSeconds)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Poll interval must be at least 1 second.");

        RuleFor(x => x)
            .Must(x => x.CommunicationType == CommunicationType.TCP || x.PollIntervalSeconds >= 30)
            .WithMessage("Poll interval must be at least 30 seconds for HTTP and HTTPS communication.");
    }
}

public sealed class UpsertDeviceAgentAdvancedSettingsRequestValidator : AbstractValidator<UpsertDeviceAgentAdvancedSettingsRequest>
{
    public UpsertDeviceAgentAdvancedSettingsRequestValidator()
    {
        RuleFor(x => x.ApplicationIntervalSeconds).GreaterThanOrEqualTo(1);
        RuleFor(x => x.DhcpPollIntervalSeconds).GreaterThanOrEqualTo(1);
        RuleFor(x => x.HeartbeatIntervalSeconds).GreaterThanOrEqualTo(1);
        RuleFor(x => x.ConnectionType).IsInEnum();
        RuleFor(x => x)
            .Must(x => x.ConnectionType == CommunicationType.TCP || x.HeartbeatIntervalSeconds >= 30)
            .WithMessage("Heartbeat interval must be at least 30 seconds for HTTP and HTTPS communication.");
    }
}

public sealed class UpsertGroupRemoteSettingsRequestValidator : AbstractValidator<UpsertGroupRemoteSettingsRequest>
{
    public UpsertGroupRemoteSettingsRequestValidator()
    {
        RuleFor(x => x.ServerHost).MaximumLength(255);
        RuleFor(x => x.ServerPort).InclusiveBetween(1, 65535);
        RuleFor(x => x.PollIntervalSeconds).GreaterThanOrEqualTo(1);
        RuleFor(x => x)
            .Must(x => x.CommunicationType == CommunicationType.TCP || x.PollIntervalSeconds >= 30)
            .WithMessage("Poll interval must be at least 30 seconds for HTTP and HTTPS communication.");
    }
}

public sealed class UpsertGroupAgentAdvancedSettingsRequestValidator : AbstractValidator<UpsertGroupAgentAdvancedSettingsRequest>
{
    public UpsertGroupAgentAdvancedSettingsRequestValidator()
    {
        RuleFor(x => x.ApplicationIntervalSeconds).GreaterThanOrEqualTo(1);
        RuleFor(x => x.DhcpPollIntervalSeconds).GreaterThanOrEqualTo(1);
        RuleFor(x => x.HeartbeatIntervalSeconds).GreaterThanOrEqualTo(1);
        RuleFor(x => x)
            .Must(x => x.ConnectionType == CommunicationType.TCP || x.HeartbeatIntervalSeconds >= 30)
            .WithMessage("Heartbeat interval must be at least 30 seconds for HTTP and HTTPS communication.");
    }
}

public sealed class AgentConfigAckRequestValidator : AbstractValidator<AgentConfigAckRequest>
{
    public AgentConfigAckRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.GeneralApplied || x.AdvancedApplied)
            .WithMessage("At least one of generalApplied or advancedApplied must be true.");
    }
}

public sealed class PatchDeviceSettingsInheritanceRequestValidator : AbstractValidator<PatchDeviceSettingsInheritanceRequest>
{
    public PatchDeviceSettingsInheritanceRequestValidator()
    {
        // bool field always valid
    }
}

public sealed class SystemSettingExecuteNowRequestValidator : AbstractValidator<SystemSettingExecuteNowRequest>
{
    public SystemSettingExecuteNowRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new SystemSettingTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new SystemSettingRemoteSettingsRequestValidator());
        RuleFor(x => x.Execution).SetValidator(new SystemSettingExecutionRequestValidator("InstantApply"));
    }

    internal static string? ExtractOsSuffix(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
        {
            return null;
        }

        var trimmed = macAddress.Trim();
        var idx = trimmed.LastIndexOf(':');
        if (idx < 0 || idx == trimmed.Length - 1)
        {
            return null;
        }

        return trimmed[(idx + 1)..].ToUpperInvariant();
    }

    private static bool HaveSupportedOsSuffix(string macAddress)
    {
        var suffix = ExtractOsSuffix(macAddress);
        return suffix is "XP" or "LX" or "CE";
    }

    private static bool MatchMacSuffixAndOsType(SystemSettingExecuteNowRequest request)
    {
        var suffix = ExtractOsSuffix(request.Target.MacAddress);
        if (suffix is null || string.IsNullOrWhiteSpace(request.Target.OsType))
        {
            return false;
        }

        return suffix == request.Target.OsType.Trim().ToUpperInvariant();
    }
}

public sealed class SystemSettingExecuteNowBulkRequestValidator : AbstractValidator<SystemSettingExecuteNowBulkRequest>
{
    public SystemSettingExecuteNowBulkRequestValidator()
    {
        RuleFor(x => x.Targets)
            .NotEmpty()
            .WithMessage("At least one target is required.");

        RuleForEach(x => x.Targets).SetValidator(new SystemSettingTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new SystemSettingRemoteSettingsRequestValidator());
        RuleFor(x => x.Execution).SetValidator(new SystemSettingExecutionRequestValidator("InstantApply"));

        RuleFor(x => x.Targets)
            .Must(HaveSingleOsType)
            .WithMessage("All targets must share the same osType.");
    }

    private static bool HaveSingleOsType(List<SystemSettingTargetRequest> targets)
    {
        var osTypes = targets
            .Select(t => t.OsType.Trim().ToUpperInvariant())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .ToList();

        return osTypes.Count <= 1;
    }
}

public sealed class SystemSettingQueueRequestValidator : AbstractValidator<SystemSettingQueueRequest>
{
    public SystemSettingQueueRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new SystemSettingTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new SystemSettingRemoteSettingsRequestValidator());
        RuleFor(x => x.Execution).SetValidator(new SystemSettingExecutionRequestValidator("Queue"));
    }
}

public sealed class SystemSettingTemplateQueueRequestValidator : AbstractValidator<SystemSettingTemplateQueueRequest>
{
    public SystemSettingTemplateQueueRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new SystemSettingTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new SystemSettingRemoteSettingsRequestValidator());
        RuleFor(x => x.Execution).SetValidator(new SystemSettingExecutionRequestValidator("QueueTemplate"));

        RuleFor(x => x.Execution.TemplateId)
            .NotNull()
            .GreaterThan(0)
            .WithMessage("templateId must be greater than 0.");

        RuleFor(x => x.Execution.TemplateName)
            .NotEmpty()
            .WithMessage("templateName is required.");
    }
}

public sealed class SystemSettingTargetRequestValidator : AbstractValidator<SystemSettingTargetRequest>
{
    public SystemSettingTargetRequestValidator()
    {
        RuleFor(x => x.MacAddress)
            .NotEmpty()
            .MaximumLength(300)
            .Must(HaveSupportedOsSuffix)
            .WithMessage("macAddress must include a supported suffix (:XP, :LX, :CE).");

        RuleFor(x => x.OsType)
            .NotEmpty()
            .Must(os => os.Trim().ToUpperInvariant() is "XP" or "LX" or "CE")
            .WithMessage("osType must be one of XP, LX, or CE.");

        RuleFor(x => x)
            .Must(x =>
            {
                var suffix = SystemSettingExecuteNowRequestValidator.ExtractOsSuffix(x.MacAddress);
                return suffix is not null && suffix == x.OsType.Trim().ToUpperInvariant();
            })
            .WithMessage("target.osType must match macAddress suffix.");
    }

    private static bool HaveSupportedOsSuffix(string macAddress)
    {
        var suffix = SystemSettingExecuteNowRequestValidator.ExtractOsSuffix(macAddress);
        return suffix is "XP" or "LX" or "CE";
    }
}

public sealed class SystemSettingRemoteSettingsRequestValidator : AbstractValidator<SystemSettingRemoteSettingsRequest>
{
    public SystemSettingRemoteSettingsRequestValidator()
    {
        RuleFor(x => x.ServerIpOrHost).NotEmpty().MaximumLength(255);
        RuleFor(x => x.PortNo).InclusiveBetween(1, 65535);
        RuleFor(x => x.HeartbeatIntervalSeconds).InclusiveBetween(30, 86400);
        RuleFor(x => x.CommunicationType)
            .Must(t => t is CommunicationType.HTTP or CommunicationType.HTTPS)
            .WithMessage("communicationType must be HTTP or HTTPS.");
    }
}

internal sealed class SystemSettingExecutionRequestValidator : AbstractValidator<SystemSettingExecutionRequest>
{
    public SystemSettingExecutionRequestValidator(string scheduleType)
    {
        RuleFor(x => x.ScheduleType)
            .Equal(scheduleType)
            .WithMessage($"scheduleType must be {scheduleType} for this endpoint.");
    }
}

public sealed class SystemSettingHistoryQueryValidator : AbstractValidator<SystemSettingHistoryQuery>
{
    public SystemSettingHistoryQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) || s is "Pending" or "Delivered" or "Applied" or "Failed")
            .WithMessage("status must be one of Pending, Delivered, Applied, Failed.");

        RuleFor(x => x)
            .Must(x => !x.FromUtc.HasValue || !x.ToUtc.HasValue || x.FromUtc <= x.ToUtc)
            .WithMessage("fromUtc must be less than or equal to toUtc.");
    }
}

public sealed class KeyboardExecuteNowRequestValidator : AbstractValidator<KeyboardExecuteNowRequest>
{
    public KeyboardExecuteNowRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new KeyboardTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new KeyboardSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
    }
}

public sealed class KeyboardQueueRequestValidator : AbstractValidator<KeyboardQueueRequest>
{
    public KeyboardQueueRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new KeyboardTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new KeyboardSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "Queue", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be Queue for this endpoint.");
    }
}

public sealed class KeyboardTargetRequestValidator : AbstractValidator<KeyboardTargetRequest>
{
    public KeyboardTargetRequestValidator()
    {
        RuleFor(x => x.MacAddress)
            .NotEmpty()
            .MaximumLength(300)
            .Must(HaveSupportedOsSuffix)
            .WithMessage("macAddress must include a supported suffix (:XP, :LX, :CE).");

        RuleFor(x => x.OsType)
            .NotEmpty()
            .Must(os => os.Trim().ToUpperInvariant() is "XP" or "LX" or "CE")
            .WithMessage("osType must be one of XP, LX, or CE.");

        RuleFor(x => x)
            .Must(x =>
            {
                var suffix = SystemSettingExecuteNowRequestValidator.ExtractOsSuffix(x.MacAddress);
                return suffix is not null && suffix == x.OsType.Trim().ToUpperInvariant();
            })
            .WithMessage("target.osType must match macAddress suffix.");
    }

    private static bool HaveSupportedOsSuffix(string macAddress)
    {
        var suffix = SystemSettingExecuteNowRequestValidator.ExtractOsSuffix(macAddress);
        return suffix is "XP" or "LX" or "CE";
    }
}

public sealed class KeyboardSettingsRequestValidator : AbstractValidator<KeyboardSettingsRequest>
{
    public KeyboardSettingsRequestValidator()
    {
        RuleFor(x => x.Delay).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RepeatRate).InclusiveBetween(0, 31);
        RuleFor(x => x.KeyboardLocale).NotEmpty().MaximumLength(100);
    }
}

public sealed class KeyboardHistoryQueryValidator : AbstractValidator<KeyboardHistoryQuery>
{
    public KeyboardHistoryQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) || s is "Pending" or "Delivered" or "Applied" or "Failed")
            .WithMessage("status must be one of Pending, Delivered, Applied, Failed.");

        RuleFor(x => x)
            .Must(x => !x.FromUtc.HasValue || !x.ToUtc.HasValue || x.FromUtc <= x.ToUtc)
            .WithMessage("fromUtc must be less than or equal to toUtc.");
    }
}

public sealed class MouseExecuteNowRequestValidator : AbstractValidator<MouseExecuteNowRequest>
{
    public MouseExecuteNowRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new MouseTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new MouseSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
    }
}

public sealed class MouseQueueRequestValidator : AbstractValidator<MouseQueueRequest>
{
    public MouseQueueRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new MouseTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new MouseSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "Queue", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be Queue for this endpoint.");
    }
}

public sealed class MouseTargetRequestValidator : AbstractValidator<MouseTargetRequest>
{
    public MouseTargetRequestValidator()
    {
        RuleFor(x => x.MacAddress)
            .NotEmpty()
            .MaximumLength(300)
            .Must(HaveSupportedOsSuffix)
            .WithMessage("macAddress must include a supported suffix (:XP, :LX, :CE).");

        RuleFor(x => x.OsType)
            .NotEmpty()
            .Must(os => os.Trim().ToUpperInvariant() is "XP" or "LX" or "CE")
            .WithMessage("osType must be one of XP, LX, or CE.");

        RuleFor(x => x)
            .Must(x =>
            {
                var suffix = SystemSettingExecuteNowRequestValidator.ExtractOsSuffix(x.MacAddress);
                return suffix is not null && suffix == x.OsType.Trim().ToUpperInvariant();
            })
            .WithMessage("target.osType must match macAddress suffix.");
    }

    private static bool HaveSupportedOsSuffix(string macAddress)
    {
        var suffix = SystemSettingExecuteNowRequestValidator.ExtractOsSuffix(macAddress);
        return suffix is "XP" or "LX" or "CE";
    }
}

public sealed class MouseSettingsRequestValidator : AbstractValidator<MouseSettingsRequest>
{
    public MouseSettingsRequestValidator()
    {
        RuleFor(x => x.PointerSpeed).InclusiveBetween(0, 100);
        RuleFor(x => x.DoubleClickSpeed).InclusiveBetween(0, 100);
    }
}

public sealed class MouseHistoryQueryValidator : AbstractValidator<MouseHistoryQuery>
{
    public MouseHistoryQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) || s is "Pending" or "Delivered" or "Applied" or "Failed")
            .WithMessage("status must be one of Pending, Delivered, Applied, Failed.");

        RuleFor(x => x)
            .Must(x => !x.FromUtc.HasValue || !x.ToUtc.HasValue || x.FromUtc <= x.ToUtc)
            .WithMessage("fromUtc must be less than or equal to toUtc.");
    }
}

public sealed class DisplayExecuteNowRequestValidator : AbstractValidator<DisplayExecuteNowRequest>
{
    public DisplayExecuteNowRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new DisplayTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new DisplaySettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
    }
}

public sealed class DisplayQueueRequestValidator : AbstractValidator<DisplayQueueRequest>
{
    public DisplayQueueRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new DisplayTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new DisplaySettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "Queue", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be Queue for this endpoint.");
    }
}

public sealed class DisplayTargetRequestValidator : AbstractValidator<DisplayTargetRequest>
{
    public DisplayTargetRequestValidator()
    {
        RuleFor(x => x.MacAddress)
            .NotEmpty()
            .MaximumLength(300)
            .Must(HaveSupportedOsSuffix)
            .WithMessage("macAddress must include a supported suffix (:XP, :LX, :CE).");

        RuleFor(x => x.OsType)
            .NotEmpty()
            .Must(os => os.Trim().ToUpperInvariant() is "XP" or "LX" or "CE")
            .WithMessage("osType must be one of XP, LX, or CE.");

        RuleFor(x => x)
            .Must(x =>
            {
                var suffix = SystemSettingExecuteNowRequestValidator.ExtractOsSuffix(x.MacAddress);
                return suffix is not null && suffix == x.OsType.Trim().ToUpperInvariant();
            })
            .WithMessage("target.osType must match macAddress suffix.");
    }

    private static bool HaveSupportedOsSuffix(string macAddress)
    {
        var suffix = SystemSettingExecuteNowRequestValidator.ExtractOsSuffix(macAddress);
        return suffix is "XP" or "LX" or "CE";
    }
}

public sealed class DisplaySettingsRequestValidator : AbstractValidator<DisplaySettingsRequest>
{
    public DisplaySettingsRequestValidator()
    {
        RuleFor(x => x.Resolution).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ColorDepth).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DualDisplayOption).MaximumLength(100);
        RuleFor(x => x.SecondaryRotation).MaximumLength(50);
    }
}

public sealed class DisplayHistoryQueryValidator : AbstractValidator<DisplayHistoryQuery>
{
    public DisplayHistoryQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) || s is "Pending" or "Delivered" or "Applied" or "Failed")
            .WithMessage("status must be one of Pending, Delivered, Applied, Failed.");

        RuleFor(x => x)
            .Must(x => !x.FromUtc.HasValue || !x.ToUtc.HasValue || x.FromUtc <= x.ToUtc)
            .WithMessage("fromUtc must be less than or equal to toUtc.");
    }
}

public sealed class Windows8021xExecuteNowRequestValidator : AbstractValidator<Windows8021xExecuteNowRequest>
{
    public Windows8021xExecuteNowRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new Windows8021xTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new Windows8021xSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
    }
}

public sealed class Windows8021xQueueRequestValidator : AbstractValidator<Windows8021xQueueRequest>
{
    public Windows8021xQueueRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new Windows8021xTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new Windows8021xSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "Queue", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be Queue for this endpoint.");
    }
}

public sealed class Windows8021xTargetRequestValidator : AbstractValidator<Windows8021xTargetRequest>
{
    public Windows8021xTargetRequestValidator()
    {
        RuleFor(x => x.MacAddress)
            .NotEmpty()
            .MaximumLength(300)
            .Must(mac => string.Equals(SystemSettingExecuteNowRequestValidator.ExtractOsSuffix(mac), "XP", StringComparison.OrdinalIgnoreCase))
            .WithMessage("macAddress must include :XP suffix (Windows only in v1).");

        RuleFor(x => x.OsType)
            .NotEmpty()
            .Must(os => string.Equals(os.Trim(), "XP", StringComparison.OrdinalIgnoreCase))
            .WithMessage("osType must be XP.");

        RuleFor(x => x)
            .Must(x =>
            {
                var suffix = SystemSettingExecuteNowRequestValidator.ExtractOsSuffix(x.MacAddress);
                return suffix is not null && suffix == x.OsType.Trim().ToUpperInvariant();
            })
            .WithMessage("target.osType must match macAddress suffix.");
    }
}

public sealed class Windows8021xSettingsRequestValidator : AbstractValidator<Windows8021xSettingsRequest>
{
    public const int MaxSettingsJsonLength = 32_000;
    public const int MaxPasswordLength = 256;

    public Windows8021xSettingsRequestValidator()
    {
        RuleFor(x => x.SettingsJson)
            .NotEmpty()
            .MaximumLength(MaxSettingsJsonLength)
            .Must(Windows8021xSettingsRequestValidation.IsValidJsonObject)
            .WithMessage("settingsJson must be a valid JSON object.");

        RuleFor(x => x.SettingsJson)
            .Must(Windows8021xSettingsRequestValidation.HasAuthenticationFlag)
            .WithMessage("settingsJson must contain blEnable802_Authentication (boolean).");

        RuleFor(x => x.SettingsJson)
            .Must(Windows8021xSettingsRequestValidation.HasAuthenticationMethodWhenEnabled)
            .WithMessage("str_Authentication is required when blEnable802_Authentication is true.");

        RuleFor(x => x.SettingsJson)
            .Must(Windows8021xSettingsRequestValidation.PasswordWithinLimit)
            .WithMessage($"cPassword must not exceed {MaxPasswordLength} characters.");
    }
}

internal static class Windows8021xSettingsRequestValidation
{
    public static bool IsValidJsonObject(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return false;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(settingsJson);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    public static bool HasAuthenticationFlag(string? settingsJson)
    {
        if (!TryGetRoot(settingsJson, out var root))
        {
            return false;
        }

        return root.TryGetProperty("blEnable802_Authentication", out var enabled) &&
               (enabled.ValueKind == System.Text.Json.JsonValueKind.True ||
                enabled.ValueKind == System.Text.Json.JsonValueKind.False);
    }

    public static bool HasAuthenticationMethodWhenEnabled(string? settingsJson)
    {
        if (!TryGetRoot(settingsJson, out var root))
        {
            return false;
        }

        if (!root.TryGetProperty("blEnable802_Authentication", out var enabled) ||
            enabled.ValueKind != System.Text.Json.JsonValueKind.True)
        {
            return true;
        }

        return root.TryGetProperty("str_Authentication", out var method) &&
               method.ValueKind == System.Text.Json.JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(method.GetString());
    }

    public static bool PasswordWithinLimit(string? settingsJson)
    {
        if (!TryGetRoot(settingsJson, out var root))
        {
            return false;
        }

        if (!root.TryGetProperty(Windows8021xSensitiveFields.PasswordPropertyName, out var password) ||
            password.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            return true;
        }

        var value = password.GetString() ?? string.Empty;
        return value.Length <= Windows8021xSettingsRequestValidator.MaxPasswordLength;
    }

    private static bool TryGetRoot(string? settingsJson, out System.Text.Json.JsonElement root)
    {
        root = default;
        if (!IsValidJsonObject(settingsJson))
        {
            return false;
        }

        using var document = System.Text.Json.JsonDocument.Parse(settingsJson!);
        root = document.RootElement.Clone();
        return true;
    }
}

public sealed class Windows8021xHistoryQueryValidator : AbstractValidator<Windows8021xHistoryQuery>
{
    public Windows8021xHistoryQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) || s is "Pending" or "Delivered" or "Applied" or "Failed")
            .WithMessage("status must be one of Pending, Delivered, Applied, Failed.");

        RuleFor(x => x)
            .Must(x => !x.FromUtc.HasValue || !x.ToUtc.HasValue || x.FromUtc <= x.ToUtc)
            .WithMessage("fromUtc must be less than or equal to toUtc.");
    }
}

public sealed class WindowsComputerNameExecuteNowRequestValidator : AbstractValidator<WindowsComputerNameExecuteNowRequest>
{
    public WindowsComputerNameExecuteNowRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsComputerNameTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsComputerNameSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsComputerNameSettingsRequestValidation.PayloadWithinLimit(x.Settings, x.Target.MacAddress))
            .WithMessage($"Serialized agent payload exceeds {WindowsComputerNameSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }
}

public sealed class WindowsComputerNameQueueRequestValidator : AbstractValidator<WindowsComputerNameQueueRequest>
{
    public WindowsComputerNameQueueRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsComputerNameTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsComputerNameSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "Queue", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be Queue for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsComputerNameSettingsRequestValidation.PayloadWithinLimit(x.Settings, x.Target.MacAddress))
            .WithMessage($"Serialized agent payload exceeds {WindowsComputerNameSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }
}

public sealed class WindowsComputerNameTargetRequestValidator : AbstractValidator<WindowsComputerNameTargetRequest>
{
    public WindowsComputerNameTargetRequestValidator()
    {
        RuleFor(x => x.MacAddress)
            .NotEmpty()
            .MaximumLength(300)
            .Must(HaveXpOsSuffix)
            .WithMessage("macAddress must include :XP suffix.");

        RuleFor(x => x.OsType)
            .NotEmpty()
            .Must(os => string.Equals(os.Trim(), "XP", StringComparison.OrdinalIgnoreCase))
            .WithMessage("osType must be XP.");

        RuleFor(x => x)
            .Must(x =>
            {
                var suffix = SystemSettingExecuteNowRequestValidator.ExtractOsSuffix(x.MacAddress);
                return suffix == "XP";
            })
            .WithMessage("target.osType must match macAddress suffix.");
    }

    private static bool HaveXpOsSuffix(string macAddress)
    {
        var suffix = SystemSettingExecuteNowRequestValidator.ExtractOsSuffix(macAddress);
        return suffix == "XP";
    }
}

public sealed class WindowsComputerNameSettingsRequestValidator : AbstractValidator<WindowsComputerNameSettingsRequest>
{
    public const int MaxHostNameLength = 15;
    public const int MaxDomainLength = 253;
    public const int MaxWorkGroupLength = 15;
    public const int MaxUserNameLength = 256;
    public const int MaxPasswordLength = 64;
    public const int MaxOrganizationalUnitLength = 100;
    public const int MaxPrefixLength = 20;
    public const int MaxPostfixLength = 20;
    public const int MaxNoOfChar = 15;

    public WindowsComputerNameSettingsRequestValidator()
    {
        RuleFor(x => x.ApplyMode).IsInEnum();

        RuleFor(x => x.HostName).MaximumLength(MaxHostNameLength);
        RuleFor(x => x.Domain).MaximumLength(MaxDomainLength);
        RuleFor(x => x.WorkGroup).MaximumLength(MaxWorkGroupLength);
        RuleFor(x => x.OrganizationalUnit).MaximumLength(MaxOrganizationalUnitLength);
        RuleFor(x => x.UserName).MaximumLength(MaxUserNameLength);
        RuleFor(x => x.Password).MaximumLength(MaxPasswordLength);
        RuleFor(x => x.Prefix).MaximumLength(MaxPrefixLength);
        RuleFor(x => x.Postfix).MaximumLength(MaxPostfixLength);
        RuleFor(x => x.NoOfChar).InclusiveBetween(0, MaxNoOfChar);

        RuleFor(x => x)
            .Must(HasHostRenameIdentity)
            .When(x => x.ApplyMode == ComputerNameApplyMode.HostRename)
            .WithMessage("hostName is required unless auto-generate metadata is provided (prefix, postfix, or noOfChar).");

        RuleFor(x => x.HostName)
            .NotEmpty()
            .When(x => x.ApplyMode == ComputerNameApplyMode.DomainJoin)
            .WithMessage("hostName is required for domain join.");

        RuleFor(x => x)
            .Must(HasValidDomainJoinFields)
            .When(x => x.ApplyMode == ComputerNameApplyMode.DomainJoin)
            .WithMessage("domain join requires domain credentials or workgroup credentials based on isDomainJoin.");
    }

    private static bool HasHostRenameIdentity(WindowsComputerNameSettingsRequest settings) =>
        !string.IsNullOrWhiteSpace(settings.HostName) ||
        !string.IsNullOrWhiteSpace(settings.Prefix) ||
        !string.IsNullOrWhiteSpace(settings.Postfix) ||
        settings.NoOfChar > 0;

    private static bool HasValidDomainJoinFields(WindowsComputerNameSettingsRequest settings)
    {
        if (settings.IsDomainJoin)
        {
            return !string.IsNullOrWhiteSpace(settings.Domain) &&
                   !string.IsNullOrWhiteSpace(settings.UserName) &&
                   !string.IsNullOrWhiteSpace(settings.Password) &&
                   string.IsNullOrWhiteSpace(settings.WorkGroup);
        }

        return !string.IsNullOrWhiteSpace(settings.WorkGroup) &&
               !string.IsNullOrWhiteSpace(settings.UserName) &&
               !string.IsNullOrWhiteSpace(settings.Password) &&
               string.IsNullOrWhiteSpace(settings.Domain) &&
               string.IsNullOrWhiteSpace(settings.OrganizationalUnit);
    }
}

public sealed class WindowsComputerNameHistoryQueryValidator : AbstractValidator<WindowsComputerNameHistoryQuery>
{
    public WindowsComputerNameHistoryQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) || s is "Pending" or "Delivered" or "Applied" or "Failed")
            .WithMessage("status must be one of Pending, Delivered, Applied, Failed.");

        RuleFor(x => x)
            .Must(x => !x.FromUtc.HasValue || !x.ToUtc.HasValue || x.FromUtc <= x.ToUtc)
            .WithMessage("fromUtc must be less than or equal to toUtc.");
    }
}

/// <summary>
/// Payload sizing for FluentValidation — must stay aligned with WindowsComputerNamePayloadBuilder.
/// </summary>
internal static class WindowsComputerNameSettingsRequestValidation
{
    public const int MaxFunctionParameterLength = 512;

    private static readonly string[] EmptyTextFields = ["", "", "", "", ""];

    public static bool PayloadWithinLimit(WindowsComputerNameSettingsRequest settings, string macAddress)
    {
        var macAddr = MapMacAddress(macAddress);
        var payload = settings.ApplyMode == ComputerNameApplyMode.HostRename
            ? SerializeHostRename(settings, macAddr)
            : SerializeDomainJoin(settings, macAddr);

        return payload.Length <= MaxFunctionParameterLength;
    }

    private static string MapMacAddress(string deviceMacAddress) =>
        deviceMacAddress.Trim().EndsWith(":XP", StringComparison.OrdinalIgnoreCase)
            ? deviceMacAddress.Trim()
            : $"{deviceMacAddress.Trim()}:XP";

    private static string SerializeHostRename(WindowsComputerNameSettingsRequest settings, string macAddr)
    {
        var inner = new
        {
            MacAddr = macAddr,
            HostName = settings.HostName.Trim(),
            Domain = string.Empty,
            WorkGroup = string.Empty,
            UserName = string.Empty,
            Password = string.Empty,
            prefix = settings.Prefix.Trim(),
            postfix = settings.Postfix.Trim(),
            noOfChar = settings.NoOfChar,
            IsMacOrSrNo = settings.IsMacOrSerial,
            Text1 = EmptyTextFields[0],
            Text2 = EmptyTextFields[1],
            Text3 = EmptyTextFields[2],
            Text4 = EmptyTextFields[3],
            Text5 = EmptyTextFields[4],
            TaskID = 0,
            AgentAction = 0
        };

        return System.Text.Json.JsonSerializer.Serialize(new { WinCELinux = new { WindowsComputerNameSettings = inner } });
    }

    private static string SerializeDomainJoin(WindowsComputerNameSettingsRequest settings, string macAddr)
    {
        var inner = new
        {
            MacAddr = macAddr,
            IsDomainWorkgroup = settings.IsDomainJoin ? "True" : "False",
            HostName = settings.HostName.Trim(),
            Domain = settings.IsDomainJoin ? settings.Domain.Trim() : string.Empty,
            WorkGroup = settings.IsDomainJoin ? string.Empty : settings.WorkGroup.Trim(),
            UserName = settings.UserName.Trim(),
            Password = settings.Password,
            OrganizationalUnit = settings.IsDomainJoin ? settings.OrganizationalUnit.Trim() : string.Empty,
            Text1 = EmptyTextFields[0],
            Text2 = EmptyTextFields[1],
            Text3 = EmptyTextFields[2],
            Text4 = EmptyTextFields[3],
            Text5 = EmptyTextFields[4],
            TaskID = 0,
            AgentAction = 0
        };

        return System.Text.Json.JsonSerializer.Serialize(new { WinCELinux = new { WindowsDomainSettings = inner } });
    }
}

public sealed class WindowsComputerNameExecuteNowBulkRequestValidator : AbstractValidator<WindowsComputerNameExecuteNowBulkRequest>
{
    public const int MaxTargets = 500;

    public WindowsComputerNameExecuteNowBulkRequestValidator()
    {
        RuleFor(x => x.Targets)
            .NotEmpty()
            .WithMessage("At least one target is required.");

        RuleFor(x => x.Targets)
            .Must(targets => targets.Count <= MaxTargets)
            .WithMessage($"At most {MaxTargets} targets are allowed.");

        RuleForEach(x => x.Targets).SetValidator(new WindowsComputerNameTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsComputerNameSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");

        RuleFor(x => x)
            .Must(x => WindowsComputerNameSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                WindowsComputerNameTestValidationMacAddress.Value))
            .WithMessage($"Serialized agent payload exceeds {WindowsComputerNameSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }
}

public sealed class WindowsComputerNameExecuteNowGroupRequestValidator : AbstractValidator<WindowsComputerNameExecuteNowGroupRequest>
{
    public WindowsComputerNameExecuteNowGroupRequestValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Settings).SetValidator(new WindowsComputerNameSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");

        RuleFor(x => x)
            .Must(x => WindowsComputerNameSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                WindowsComputerNameTestValidationMacAddress.Value))
            .WithMessage($"Serialized agent payload exceeds {WindowsComputerNameSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }
}

internal static class WindowsComputerNameTestValidationMacAddress
{
    public const string Value = "AA:BB:CC:DD:EE:10:XP";
}