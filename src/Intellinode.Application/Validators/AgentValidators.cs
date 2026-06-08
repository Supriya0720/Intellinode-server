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