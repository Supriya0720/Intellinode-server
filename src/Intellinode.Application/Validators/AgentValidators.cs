using System.Net;
using System.Net.Sockets;
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

public sealed class WindowsDateTimeExecuteNowRequestValidator : AbstractValidator<WindowsDateTimeExecuteNowRequest>
{
    public WindowsDateTimeExecuteNowRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsDateTimeTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsDateTimeSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsDateTimeSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsDateTimeSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsDateTimeQueueRequestValidator : AbstractValidator<WindowsDateTimeQueueRequest>
{
    public WindowsDateTimeQueueRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsDateTimeTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsDateTimeSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "Queue", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be Queue for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsDateTimeSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsDateTimeSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsDateTimeTargetRequestValidator : AbstractValidator<WindowsDateTimeTargetRequest>
{
    public WindowsDateTimeTargetRequestValidator()
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

public sealed class WindowsDateTimeSettingsRequestValidator : AbstractValidator<WindowsDateTimeSettingsRequest>
{
    public const int MaxTimeZoneDisplayLength = 200;
    public const int MaxWindowsTzKeyLength = 50;
    public const int MaxTimeServerLength = 255;
    private static readonly System.Text.RegularExpressions.Regex TimePattern =
        new(@"^([01]?[0-9]|2[0-3]):[0-5][0-9]$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public WindowsDateTimeSettingsRequestValidator()
    {
        RuleFor(x => x.ApplyMode).IsInEnum();
        RuleFor(x => x.TimeZoneDisplay).MaximumLength(MaxTimeZoneDisplayLength);
        RuleFor(x => x.WindowsTzKey).MaximumLength(MaxWindowsTzKeyLength);
        RuleFor(x => x.TimeServer).MaximumLength(MaxTimeServerLength);

        RuleFor(x => x.CurrentDateLocal)
            .NotNull()
            .When(x => x.ApplyMode == WindowsDateTimeApplyMode.ManualDateTime)
            .WithMessage("currentDateLocal is required for ManualDateTime.");

        RuleFor(x => x.CurrentTimeLocal)
            .NotEmpty()
            .When(x => x.ApplyMode == WindowsDateTimeApplyMode.ManualDateTime)
            .WithMessage("currentTimeLocal is required for ManualDateTime.");

        RuleFor(x => x.CurrentTimeLocal)
            .Must(t => t is not null && TimePattern.IsMatch(t.Trim()))
            .When(x => x.ApplyMode == WindowsDateTimeApplyMode.ManualDateTime && !string.IsNullOrWhiteSpace(x.CurrentTimeLocal))
            .WithMessage("currentTimeLocal must match 24-hour HH:mm format.");

        RuleFor(x => x.TimeZoneDisplay)
            .NotEmpty()
            .When(x => x.ApplyMode == WindowsDateTimeApplyMode.TimeZone)
            .WithMessage("timeZoneDisplay is required for TimeZone.");

        RuleFor(x => x.WindowsTzKey)
            .NotNull()
            .When(x => x.ApplyMode == WindowsDateTimeApplyMode.TimeZone)
            .WithMessage("windowsTzKey is required for TimeZone.");

        RuleFor(x => x.TimeServer)
            .NotEmpty()
            .When(x => x.ApplyMode == WindowsDateTimeApplyMode.TimeServer)
            .WithMessage("timeServer is required for TimeServer.");
    }
}

public sealed class WindowsDateTimeHistoryQueryValidator : AbstractValidator<WindowsDateTimeHistoryQuery>
{
    public WindowsDateTimeHistoryQueryValidator()
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

internal static class WindowsDateTimeSettingsRequestValidation
{
    public const int MaxFunctionParameterLength = 512;

    public static bool PayloadWithinLimit(WindowsDateTimeSettingsRequest settings, int agentAction)
    {
        var parsedTime = ParseTimeLocal(settings.CurrentTimeLocal);
        var payload = SerializePayload(settings, parsedTime, agentAction);
        return payload.Length <= MaxFunctionParameterLength;
    }

    private static TimeOnly? ParseTimeLocal(string? currentTimeLocal)
    {
        if (string.IsNullOrWhiteSpace(currentTimeLocal))
        {
            return null;
        }

        if (TimeOnly.TryParseExact(currentTimeLocal.Trim(), "HH:mm", out var hhmm))
        {
            return hhmm;
        }

        return TimeOnly.TryParseExact(currentTimeLocal.Trim(), "H:mm", out var hmm) ? hmm : null;
    }

    private static string SerializePayload(
        WindowsDateTimeSettingsRequest settings,
        TimeOnly? parsedTime,
        int agentAction)
    {
        var strTimeZone = string.Empty;
        var dtDate = string.Empty;
        var dtTime = string.Empty;
        var timeServer = string.Empty;
        var muiDisplay = string.Empty;

        switch (settings.ApplyMode)
        {
            case WindowsDateTimeApplyMode.ManualDateTime:
                if (settings.CurrentDateLocal is { } date && parsedTime is { } time)
                {
                    dtDate = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0)
                        .ToString("yyyy-MM-ddTHH:mm:ss");
                    dtTime = new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second)
                        .ToString("yyyy-MM-ddTHH:mm:ss");
                }

                break;

            case WindowsDateTimeApplyMode.TimeZone:
                strTimeZone = settings.TimeZoneDisplay?.Trim() ?? string.Empty;
                muiDisplay = settings.WindowsTzKey?.Trim() ?? string.Empty;
                break;

            case WindowsDateTimeApplyMode.TimeServer:
                timeServer = settings.TimeServer?.Trim() ?? string.Empty;
                break;
        }

        var inner = new
        {
            strTimeZone,
            DtDate = dtDate,
            DtTime = dtTime,
            TimeServer = timeServer,
            MUI_Display = muiDisplay,
            TaskID = 0,
            AgentAction = agentAction
        };

        return System.Text.Json.JsonSerializer.Serialize(new { WinCELinux = new { XPDATE_TIME = inner } });
    }
}

public sealed class WindowsDateTimeExecuteNowBulkRequestValidator : AbstractValidator<WindowsDateTimeExecuteNowBulkRequest>
{
    public const int MaxTargets = 500;

    public WindowsDateTimeExecuteNowBulkRequestValidator()
    {
        RuleFor(x => x.Targets)
            .NotEmpty()
            .WithMessage("At least one target is required.");

        RuleFor(x => x.Targets)
            .Must(targets => targets.Count <= MaxTargets)
            .WithMessage($"At most {MaxTargets} targets are allowed.");

        RuleForEach(x => x.Targets).SetValidator(new WindowsDateTimeTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsDateTimeSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");

        RuleFor(x => x)
            .Must(x => WindowsDateTimeSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                int.TryParse(x.Execution.AgentAction?.Trim(), out var value) ? value : 0))
            .WithMessage($"Serialized agent payload exceeds {WindowsDateTimeSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }
}

public sealed class WindowsDateTimeExecuteNowGroupRequestValidator : AbstractValidator<WindowsDateTimeExecuteNowGroupRequest>
{
    public WindowsDateTimeExecuteNowGroupRequestValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Settings).SetValidator(new WindowsDateTimeSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");

        RuleFor(x => x)
            .Must(x => WindowsDateTimeSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                int.TryParse(x.Execution.AgentAction?.Trim(), out var value) ? value : 0))
            .WithMessage($"Serialized agent payload exceeds {WindowsDateTimeSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }
}

public sealed class WindowsRegionLocationExecuteNowRequestValidator : AbstractValidator<WindowsRegionLocationExecuteNowRequest>
{
    public WindowsRegionLocationExecuteNowRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsRegionLocationTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsRegionLocationSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsRegionLocationSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsRegionLocationSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsRegionLocationQueueRequestValidator : AbstractValidator<WindowsRegionLocationQueueRequest>
{
    public WindowsRegionLocationQueueRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsRegionLocationTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsRegionLocationSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "Queue", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be Queue for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsRegionLocationSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsRegionLocationSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsRegionLocationTargetRequestValidator : AbstractValidator<WindowsRegionLocationTargetRequest>
{
    public WindowsRegionLocationTargetRequestValidator()
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

public sealed class WindowsRegionLocationSettingsRequestValidator : AbstractValidator<WindowsRegionLocationSettingsRequest>
{
    public const int MaxLocationNameLength = 200;
    public const int MaxBcp47CodeLength = 20;
    public const int MaxLanguageDescriptionLength = 200;

    private static readonly System.Text.RegularExpressions.Regex Bcp47Pattern =
        new(@"^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public WindowsRegionLocationSettingsRequestValidator()
    {
        RuleFor(x => x.GeoId).GreaterThan(0);
        RuleFor(x => x.LanguageCode).GreaterThan(0);
        RuleFor(x => x.LocationName)
            .NotEmpty()
            .MaximumLength(MaxLocationNameLength);
        RuleFor(x => x.Bcp47Code)
            .NotEmpty()
            .MaximumLength(MaxBcp47CodeLength)
            .Must(code => Bcp47Pattern.IsMatch(code.Trim()))
            .WithMessage("bcp47Code must be a valid BCP47-style locale tag (e.g. en-US).");
        RuleFor(x => x.LanguageDescription)
            .NotEmpty()
            .MaximumLength(MaxLanguageDescriptionLength);
    }
}

public sealed class WindowsRegionLocationHistoryQueryValidator : AbstractValidator<WindowsRegionLocationHistoryQuery>
{
    public WindowsRegionLocationHistoryQueryValidator()
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

internal static class WindowsRegionLocationSettingsRequestValidation
{
    public const int MaxFunctionParameterLength = 512;

    public static bool PayloadWithinLimit(WindowsRegionLocationSettingsRequest settings, int agentAction)
    {
        var payload = SerializePayload(settings, agentAction);
        return payload.Length <= MaxFunctionParameterLength;
    }

    private static string SerializePayload(WindowsRegionLocationSettingsRequest settings, int agentAction)
    {
        var inner = new
        {
            GeoID = settings.GeoId,
            Location = settings.LocationName?.Trim() ?? string.Empty,
            BCP47Code = settings.Bcp47Code?.Trim() ?? string.Empty,
            LanguageCode = settings.LanguageCode,
            LanguageDescription = settings.LanguageDescription?.Trim() ?? string.Empty,
            TaskID = 0,
            AgentAction = agentAction
        };

        return System.Text.Json.JsonSerializer.Serialize(new { WinCELinux = new { RegionAndLocation = inner } });
    }
}

public sealed class WindowsRegionLocationExecuteNowBulkRequestValidator : AbstractValidator<WindowsRegionLocationExecuteNowBulkRequest>
{
    public const int MaxTargets = 500;

    public WindowsRegionLocationExecuteNowBulkRequestValidator()
    {
        RuleFor(x => x.Targets)
            .NotEmpty()
            .WithMessage("At least one target is required.");

        RuleFor(x => x.Targets)
            .Must(targets => targets.Count <= MaxTargets)
            .WithMessage($"At most {MaxTargets} targets are allowed.");

        RuleForEach(x => x.Targets).SetValidator(new WindowsRegionLocationTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsRegionLocationSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");

        RuleFor(x => x)
            .Must(x => WindowsRegionLocationSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                int.TryParse(x.Execution.AgentAction?.Trim(), out var value) ? value : 0))
            .WithMessage($"Serialized agent payload exceeds {WindowsRegionLocationSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }
}

public sealed class WindowsRegionLocationExecuteNowGroupRequestValidator : AbstractValidator<WindowsRegionLocationExecuteNowGroupRequest>
{
    public WindowsRegionLocationExecuteNowGroupRequestValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Settings).SetValidator(new WindowsRegionLocationSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");

        RuleFor(x => x)
            .Must(x => WindowsRegionLocationSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                int.TryParse(x.Execution.AgentAction?.Trim(), out var value) ? value : 0))
            .WithMessage($"Serialized agent payload exceeds {WindowsRegionLocationSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }
}

public sealed class WindowsRegionalFormatExecuteNowRequestValidator : AbstractValidator<WindowsRegionalFormatExecuteNowRequest>
{
    public WindowsRegionalFormatExecuteNowRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsRegionalFormatTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsRegionalFormatSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsRegionalFormatSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsRegionalFormatSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsRegionalFormatQueueRequestValidator : AbstractValidator<WindowsRegionalFormatQueueRequest>
{
    public WindowsRegionalFormatQueueRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsRegionalFormatTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsRegionalFormatSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "Queue", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be Queue for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsRegionalFormatSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsRegionalFormatSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsRegionalFormatTargetRequestValidator : AbstractValidator<WindowsRegionalFormatTargetRequest>
{
    public WindowsRegionalFormatTargetRequestValidator()
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

public sealed class WindowsRegionalFormatSettingsRequestValidator : AbstractValidator<WindowsRegionalFormatSettingsRequest>
{
    public const int MaxTimeFormatLength = 50;
    public const int MaxSeparatorLength = 5;
    public const int MaxSymbolLength = 10;
    public const int MaxShortDateFormatLength = 50;
    public const int MaxLongDateFormatLength = 100;
    public const int MaxShortDateSampleLength = 50;
    public const int MaxLongDateSampleLength = 100;
    public const int MaxTimeSampleLength = 50;

    public WindowsRegionalFormatSettingsRequestValidator()
    {
        RuleFor(x => x.TimeFormat).NotEmpty().MaximumLength(MaxTimeFormatLength);
        RuleFor(x => x.TimeSeparator)
            .NotEmpty()
            .MinimumLength(1)
            .MaximumLength(MaxSeparatorLength);
        RuleFor(x => x.AmSymbol).NotEmpty().MaximumLength(MaxSymbolLength);
        RuleFor(x => x.PmSymbol).NotEmpty().MaximumLength(MaxSymbolLength);
        RuleFor(x => x.ShortDateFormat).NotEmpty().MaximumLength(MaxShortDateFormatLength);
        RuleFor(x => x.DateSeparator)
            .NotEmpty()
            .MinimumLength(1)
            .MaximumLength(MaxSeparatorLength);
        RuleFor(x => x.LongDateFormat).NotEmpty().MaximumLength(MaxLongDateFormatLength);
        RuleFor(x => x.ShortDateSample).NotEmpty().MaximumLength(MaxShortDateSampleLength);
        RuleFor(x => x.LongDateSample).NotEmpty().MaximumLength(MaxLongDateSampleLength);
        RuleFor(x => x.TimeSample).MaximumLength(MaxTimeSampleLength);
    }
}

public sealed class WindowsRegionalFormatHistoryQueryValidator : AbstractValidator<WindowsRegionalFormatHistoryQuery>
{
    public WindowsRegionalFormatHistoryQueryValidator()
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

internal static class WindowsRegionalFormatSettingsRequestValidation
{
    public const int MaxFunctionParameterLength = 512;

    public static bool PayloadWithinLimit(WindowsRegionalFormatSettingsRequest settings, int agentAction)
    {
        var payload = SerializePayload(settings, agentAction);
        return payload.Length <= MaxFunctionParameterLength;
    }

    private static string SerializePayload(WindowsRegionalFormatSettingsRequest settings, int agentAction)
    {
        var inner = new
        {
            strTimeFormat = settings.TimeFormat?.Trim() ?? string.Empty,
            strTimeSeperator = settings.TimeSeparator?.Trim() ?? string.Empty,
            strAMsymbol = settings.AmSymbol?.Trim() ?? string.Empty,
            strPMsymbol = settings.PmSymbol?.Trim() ?? string.Empty,
            strMinyear = string.Empty,
            strMaxyear = string.Empty,
            strShortDateFormat = settings.ShortDateFormat?.Trim() ?? string.Empty,
            strDateSeperator = settings.DateSeparator?.Trim() ?? string.Empty,
            strLongDateFormat = settings.LongDateFormat?.Trim() ?? string.Empty,
            strShortDateSample = settings.ShortDateSample?.Trim() ?? string.Empty,
            strLongDateSample = settings.LongDateSample?.Trim() ?? string.Empty,
            TaskID = 0,
            AgentAction = agentAction
        };

        return System.Text.Json.JsonSerializer.Serialize(new { WinCELinux = new { RegionalSettings = inner } });
    }
}

public sealed class WindowsRegionalFormatExecuteNowBulkRequestValidator : AbstractValidator<WindowsRegionalFormatExecuteNowBulkRequest>
{
    public const int MaxTargets = 500;

    public WindowsRegionalFormatExecuteNowBulkRequestValidator()
    {
        RuleFor(x => x.Targets)
            .NotEmpty()
            .WithMessage("At least one target is required.");

        RuleFor(x => x.Targets)
            .Must(targets => targets.Count <= MaxTargets)
            .WithMessage($"At most {MaxTargets} targets are allowed.");

        RuleForEach(x => x.Targets).SetValidator(new WindowsRegionalFormatTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsRegionalFormatSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");

        RuleFor(x => x)
            .Must(x => WindowsRegionalFormatSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                int.TryParse(x.Execution.AgentAction?.Trim(), out var value) ? value : 0))
            .WithMessage($"Serialized agent payload exceeds {WindowsRegionalFormatSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }
}

public sealed class WindowsRegionalFormatExecuteNowGroupRequestValidator : AbstractValidator<WindowsRegionalFormatExecuteNowGroupRequest>
{
    public WindowsRegionalFormatExecuteNowGroupRequestValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Settings).SetValidator(new WindowsRegionalFormatSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");

        RuleFor(x => x)
            .Must(x => WindowsRegionalFormatSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                int.TryParse(x.Execution.AgentAction?.Trim(), out var value) ? value : 0))
            .WithMessage($"Serialized agent payload exceeds {WindowsRegionalFormatSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }
}

public sealed class WindowsEthernetSetupExecuteNowRequestValidator : AbstractValidator<WindowsEthernetSetupExecuteNowRequest>
{
    public WindowsEthernetSetupExecuteNowRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsEthernetSetupTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsEthernetSetupSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsEthernetSetupSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                x.Target.MacAddress,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsEthernetSetupSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsEthernetSetupTargetRequestValidator : AbstractValidator<WindowsEthernetSetupTargetRequest>
{
    public WindowsEthernetSetupTargetRequestValidator()
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

public sealed class WindowsEthernetSetupSettingsRequestValidator : AbstractValidator<WindowsEthernetSetupSettingsRequest>
{
    public const int MaxIpFieldLength = 15;
    public const int MaxNetworkSpeedLength = 64;

    private static readonly HashSet<string> ValidSubnetOctets = new(StringComparer.Ordinal)
    {
        "0", "128", "192", "224", "240", "248", "252", "254", "255"
    };

    public WindowsEthernetSetupSettingsRequestValidator(bool requireManualIpAddress = true)
    {
        RuleFor(x => x.IpAddress).MaximumLength(MaxIpFieldLength);
        RuleFor(x => x.SubnetMask).MaximumLength(MaxIpFieldLength);
        RuleFor(x => x.Gateway).MaximumLength(MaxIpFieldLength);
        RuleFor(x => x.PrimaryDns).MaximumLength(MaxIpFieldLength);
        RuleFor(x => x.SecondaryDns).MaximumLength(MaxIpFieldLength);
        RuleFor(x => x.PrimaryWins).MaximumLength(MaxIpFieldLength);
        RuleFor(x => x.SecondaryWins).MaximumLength(MaxIpFieldLength);
        RuleFor(x => x.NetworkSpeed).MaximumLength(MaxNetworkSpeedLength);

        if (requireManualIpAddress)
        {
            RuleFor(x => x.IpAddress)
                .NotEmpty()
                .When(x => !x.IsDhcp)
                .WithMessage("ipAddress is required when isDhcp is false.");
        }

        RuleFor(x => x.SubnetMask)
            .NotEmpty()
            .When(x => !x.IsDhcp)
            .WithMessage("subnetMask is required when isDhcp is false.");

        RuleFor(x => x.Gateway)
            .NotEmpty()
            .When(x => !x.IsDhcp)
            .WithMessage("gateway is required when isDhcp is false.");

        RuleFor(x => x.IpAddress)
            .Must(IsValidIpv4)
            .When(x => !x.IsDhcp && !string.IsNullOrWhiteSpace(x.IpAddress))
            .WithMessage("ipAddress must be a valid IPv4 address.");

        RuleFor(x => x.Gateway)
            .Must(IsValidIpv4)
            .When(x => !x.IsDhcp && !string.IsNullOrWhiteSpace(x.Gateway))
            .WithMessage("gateway must be a valid IPv4 address.");

        RuleFor(x => x.SubnetMask)
            .Must(IsValidSubnetMask)
            .When(x => !x.IsDhcp && !string.IsNullOrWhiteSpace(x.SubnetMask))
            .WithMessage("subnetMask is not a valid subnet mask.");

        RuleFor(x => x.PrimaryDns)
            .Must(IsValidIpv4)
            .When(x => !x.IsDhcp && !x.ObtainDnsAutomatically && !string.IsNullOrWhiteSpace(x.PrimaryDns))
            .WithMessage("primaryDns must be a valid IPv4 address.");

        RuleFor(x => x.SecondaryDns)
            .Must(IsValidIpv4)
            .When(x => !x.IsDhcp && !x.ObtainDnsAutomatically && !string.IsNullOrWhiteSpace(x.SecondaryDns))
            .WithMessage("secondaryDns must be a valid IPv4 address.");

        RuleFor(x => x.PrimaryWins)
            .Must(IsValidIpv4)
            .When(x => !x.IsDhcp && !string.IsNullOrWhiteSpace(x.PrimaryWins))
            .WithMessage("primaryWins must be a valid IPv4 address.");

        RuleFor(x => x.SecondaryWins)
            .Must(IsValidIpv4)
            .When(x => !x.IsDhcp && !string.IsNullOrWhiteSpace(x.SecondaryWins))
            .WithMessage("secondaryWins must be a valid IPv4 address.");
    }

    internal static bool IsValidIpv4(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return IPAddress.TryParse(value.Trim(), out var ip) && ip.AddressFamily == AddressFamily.InterNetwork;
    }

    internal static bool IsValidSubnetMask(string? subnetMask)
    {
        if (string.IsNullOrWhiteSpace(subnetMask) || !IsValidIpv4(subnetMask))
        {
            return false;
        }

        var subnetval = subnetMask.Trim().Split('.');
        if (subnetval.Length != 4)
        {
            return false;
        }

        for (var j = 0; j <= 3; j++)
        {
            if (!ValidSubnetOctets.Contains(subnetval[j]))
            {
                return false;
            }

            if (j == 0 && subnetval[j] == "0")
            {
                return false;
            }

            if (j == 3 && (subnetval[j] == "254" || subnetval[j] == "255"))
            {
                return false;
            }
        }

        for (var k = 3; k > 0; k--)
        {
            if (subnetval[k] == "0")
            {
                continue;
            }

            if (subnetval[k] != "0" && subnetval[k - 1] == "255")
            {
                continue;
            }

            return false;
        }

        for (var m = 0; m < 3; m++)
        {
            if (subnetval[m] == "255")
            {
                continue;
            }

            if (subnetval[m] != "255" && subnetval[m + 1] == "0")
            {
                continue;
            }

            return false;
        }

        return true;
    }
}

/// <summary>
/// Payload sizing for FluentValidation — must stay aligned with WindowsEthernetSetupPayloadBuilder.
/// </summary>
internal static class WindowsEthernetSetupSettingsRequestValidation
{
    public const int MaxFunctionParameterLength = 512;

    public static bool PayloadWithinLimit(
        WindowsEthernetSetupSettingsRequest settings,
        string macAddress,
        int agentAction)
    {
        var macAddr = MapMacAddress(macAddress);
        var payload = SerializePayload(settings, macAddr, agentAction);
        return payload.Length <= MaxFunctionParameterLength;
    }

    private static string MapMacAddress(string deviceMacAddress) =>
        deviceMacAddress.Trim().EndsWith(":XP", StringComparison.OrdinalIgnoreCase)
            ? deviceMacAddress.Trim()
            : $"{deviceMacAddress.Trim()}:XP";

    private static string SerializePayload(
        WindowsEthernetSetupSettingsRequest settings,
        string macAddr,
        int agentAction)
    {
        var obtainDnsAutomatically = settings.ObtainDnsAutomatically;
        var priDns = obtainDnsAutomatically ? string.Empty : settings.PrimaryDns.Trim();
        var secDns = obtainDnsAutomatically ? string.Empty : settings.SecondaryDns.Trim();

        var inner = new
        {
            MacAddr = macAddr,
            DHCP = settings.IsDhcp,
            IPAddr = settings.IsDhcp ? string.Empty : settings.IpAddress.Trim(),
            SubnetMask = settings.IsDhcp ? string.Empty : settings.SubnetMask.Trim(),
            Gateway = settings.IsDhcp ? string.Empty : settings.Gateway.Trim(),
            PriDNS = settings.IsDhcp ? string.Empty : priDns,
            SecDNS = settings.IsDhcp ? string.Empty : secDns,
            PriWNS = settings.IsDhcp ? string.Empty : settings.PrimaryWins.Trim(),
            SecWNS = settings.IsDhcp ? string.Empty : settings.SecondaryWins.Trim(),
            networkSpeed = string.IsNullOrWhiteSpace(settings.NetworkSpeed) ? "AutoSelect" : settings.NetworkSpeed.Trim(),
            networkType = "Ethernet",
            IsObtainedDNSAutomatically = obtainDnsAutomatically,
            TaskID = 0,
            AgentAction = agentAction
        };

        return System.Text.Json.JsonSerializer.Serialize(new { WinCELinux = new { XPNetwork_Settings = inner } });
    }
}

public sealed class WindowsEthernetSetupQueueRequestValidator : AbstractValidator<WindowsEthernetSetupQueueRequest>
{
    public WindowsEthernetSetupQueueRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsEthernetSetupTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsEthernetSetupSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "Queue", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be Queue for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsEthernetSetupSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                x.Target.MacAddress,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsEthernetSetupSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsEthernetSetupHistoryQueryValidator : AbstractValidator<WindowsEthernetSetupHistoryQuery>
{
    public WindowsEthernetSetupHistoryQueryValidator()
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

public sealed class WindowsEthernetSetupExecuteNowBulkRequestValidator : AbstractValidator<WindowsEthernetSetupExecuteNowBulkRequest>
{
    public const int MaxTargets = 500;

    public WindowsEthernetSetupExecuteNowBulkRequestValidator()
    {
        RuleFor(x => x.Targets)
            .NotEmpty()
            .WithMessage("At least one target is required.");

        RuleFor(x => x.Targets)
            .Must(targets => targets.GroupBy(t => t.MacAddress.Trim(), StringComparer.OrdinalIgnoreCase).Count() <= MaxTargets)
            .WithMessage($"At most {MaxTargets} targets are allowed.");

        RuleForEach(x => x.Targets).SetValidator(new WindowsEthernetSetupTargetRequestValidator());

        RuleFor(x => x.Settings)
            .Custom((settings, context) =>
            {
                var bulk = (WindowsEthernetSetupExecuteNowBulkRequest)context.InstanceToValidate!;
                var uniqueCount = bulk.Targets
                    .GroupBy(t => t.MacAddress.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Count();
                var validator = new WindowsEthernetSetupSettingsRequestValidator(requireManualIpAddress: uniqueCount <= 1);
                var result = validator.Validate(settings);
                foreach (var error in result.Errors)
                {
                    context.AddFailure(error);
                }
            });

        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");

        RuleFor(x => x)
            .Must(x => WindowsEthernetSetupSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                x.Targets.FirstOrDefault()?.MacAddress ?? WindowsEthernetSetupTestValidationMacAddress.Value,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsEthernetSetupSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsEthernetSetupExecuteNowGroupRequestValidator : AbstractValidator<WindowsEthernetSetupExecuteNowGroupRequest>
{
    public WindowsEthernetSetupExecuteNowGroupRequestValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();

        RuleFor(x => x.Settings).SetValidator(new WindowsEthernetSetupSettingsRequestValidator(requireManualIpAddress: false));

        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");

        RuleFor(x => x)
            .Must(x => WindowsEthernetSetupSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                WindowsEthernetSetupTestValidationMacAddress.Value,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsEthernetSetupSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

internal static class WindowsEthernetSetupTestValidationMacAddress
{
    public const string Value = "AA:BB:CC:DD:EE:10:XP";
}

public sealed class WindowsWirelessSetupExecuteNowRequestValidator : AbstractValidator<WindowsWirelessSetupExecuteNowRequest>
{
    public WindowsWirelessSetupExecuteNowRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsWirelessSetupTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsWirelessSetupSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsWirelessSetupSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                x.Target.MacAddress,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsWirelessSetupSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsWirelessSetupTargetRequestValidator : AbstractValidator<WindowsWirelessSetupTargetRequest>
{
    public WindowsWirelessSetupTargetRequestValidator()
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

public sealed class WindowsWirelessSetupSettingsRequestValidator : AbstractValidator<WindowsWirelessSetupSettingsRequest>
{
    public const int MaxIpFieldLength = 15;

    public WindowsWirelessSetupSettingsRequestValidator(bool requireManualIpAddress = true)
    {
        RuleFor(x => x.IpAddress).MaximumLength(MaxIpFieldLength);
        RuleFor(x => x.SubnetMask).MaximumLength(MaxIpFieldLength);
        RuleFor(x => x.Gateway).MaximumLength(MaxIpFieldLength);
        RuleFor(x => x.PrimaryDns).MaximumLength(MaxIpFieldLength);
        RuleFor(x => x.SecondaryDns).MaximumLength(MaxIpFieldLength);
        RuleFor(x => x.PrimaryWins).MaximumLength(MaxIpFieldLength);
        RuleFor(x => x.SecondaryWins).MaximumLength(MaxIpFieldLength);

        if (requireManualIpAddress)
        {
            RuleFor(x => x.IpAddress)
                .NotEmpty()
                .When(x => !x.IsDhcp)
                .WithMessage("ipAddress is required when isDhcp is false.");
        }

        RuleFor(x => x.SubnetMask)
            .NotEmpty()
            .When(x => !x.IsDhcp)
            .WithMessage("subnetMask is required when isDhcp is false.");

        RuleFor(x => x.Gateway)
            .NotEmpty()
            .When(x => !x.IsDhcp)
            .WithMessage("gateway is required when isDhcp is false.");

        RuleFor(x => x.PrimaryDns)
            .NotEmpty()
            .When(x => !x.IsDhcp)
            .WithMessage("primaryDns is required when isDhcp is false.");

        RuleFor(x => x.IpAddress)
            .Must(WindowsEthernetSetupSettingsRequestValidator.IsValidIpv4)
            .When(x => !x.IsDhcp && !string.IsNullOrWhiteSpace(x.IpAddress))
            .WithMessage("ipAddress must be a valid IPv4 address.");

        RuleFor(x => x.Gateway)
            .Must(WindowsEthernetSetupSettingsRequestValidator.IsValidIpv4)
            .When(x => !x.IsDhcp && !string.IsNullOrWhiteSpace(x.Gateway))
            .WithMessage("gateway must be a valid IPv4 address.");

        RuleFor(x => x.SubnetMask)
            .Must(WindowsEthernetSetupSettingsRequestValidator.IsValidSubnetMask)
            .When(x => !x.IsDhcp && !string.IsNullOrWhiteSpace(x.SubnetMask))
            .WithMessage("subnetMask is not a valid subnet mask.");

        RuleFor(x => x.PrimaryDns)
            .Must(WindowsEthernetSetupSettingsRequestValidator.IsValidIpv4)
            .When(x => !x.IsDhcp && !string.IsNullOrWhiteSpace(x.PrimaryDns))
            .WithMessage("primaryDns must be a valid IPv4 address.");

        RuleFor(x => x.SecondaryDns)
            .Must(WindowsEthernetSetupSettingsRequestValidator.IsValidIpv4)
            .When(x => !x.IsDhcp && !string.IsNullOrWhiteSpace(x.SecondaryDns))
            .WithMessage("secondaryDns must be a valid IPv4 address.");

        RuleFor(x => x.PrimaryWins)
            .Must(WindowsEthernetSetupSettingsRequestValidator.IsValidIpv4)
            .When(x => !x.IsDhcp && !string.IsNullOrWhiteSpace(x.PrimaryWins))
            .WithMessage("primaryWins must be a valid IPv4 address.");

        RuleFor(x => x.SecondaryWins)
            .Must(WindowsEthernetSetupSettingsRequestValidator.IsValidIpv4)
            .When(x => !x.IsDhcp && !string.IsNullOrWhiteSpace(x.SecondaryWins))
            .WithMessage("secondaryWins must be a valid IPv4 address.");
    }
}

/// <summary>
/// Payload sizing for FluentValidation — must stay aligned with WindowsWirelessSetupPayloadBuilder.
/// </summary>
internal static class WindowsWirelessSetupSettingsRequestValidation
{
    public const int MaxFunctionParameterLength = 512;

    public static bool PayloadWithinLimit(
        WindowsWirelessSetupSettingsRequest settings,
        string macAddress,
        int agentAction)
    {
        var macAddr = MapMacAddress(macAddress);
        var payload = SerializePayload(settings, macAddr, agentAction);
        return payload.Length <= MaxFunctionParameterLength;
    }

    private static string MapMacAddress(string deviceMacAddress) =>
        deviceMacAddress.Trim().EndsWith(":XP", StringComparison.OrdinalIgnoreCase)
            ? deviceMacAddress.Trim()
            : $"{deviceMacAddress.Trim()}:XP";

    private static string SerializePayload(
        WindowsWirelessSetupSettingsRequest settings,
        string macAddr,
        int agentAction)
    {
        var inner = new
        {
            MacAddr = macAddr,
            DHCP = settings.IsDhcp,
            IPAddr = settings.IsDhcp ? string.Empty : settings.IpAddress.Trim(),
            SubnetMask = settings.IsDhcp ? string.Empty : settings.SubnetMask.Trim(),
            Gateway = settings.IsDhcp ? string.Empty : settings.Gateway.Trim(),
            PriDNS = settings.IsDhcp ? string.Empty : settings.PrimaryDns.Trim(),
            SecDNS = settings.IsDhcp ? string.Empty : settings.SecondaryDns.Trim(),
            PriWNS = settings.IsDhcp ? string.Empty : settings.PrimaryWins.Trim(),
            SecWNS = settings.IsDhcp ? string.Empty : settings.SecondaryWins.Trim(),
            networkType = "Wireless",
            TaskID = 0,
            AgentAction = agentAction
        };

        return System.Text.Json.JsonSerializer.Serialize(new { WinCELinux = new { XPNetwork_Settings = inner } });
    }
}

public sealed class WindowsWirelessSetupQueueRequestValidator : AbstractValidator<WindowsWirelessSetupQueueRequest>
{
    public WindowsWirelessSetupQueueRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsWirelessSetupTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsWirelessSetupSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "Queue", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be Queue for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsWirelessSetupSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                x.Target.MacAddress,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsWirelessSetupSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsWirelessSetupHistoryQueryValidator : AbstractValidator<WindowsWirelessSetupHistoryQuery>
{
    public WindowsWirelessSetupHistoryQueryValidator()
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

public sealed class WindowsWirelessSetupExecuteNowBulkRequestValidator : AbstractValidator<WindowsWirelessSetupExecuteNowBulkRequest>
{
    public const int MaxTargets = 500;

    public WindowsWirelessSetupExecuteNowBulkRequestValidator()
    {
        RuleFor(x => x.Targets)
            .NotEmpty()
            .WithMessage("At least one target is required.");

        RuleFor(x => x.Targets)
            .Must(targets => targets.GroupBy(t => t.MacAddress.Trim(), StringComparer.OrdinalIgnoreCase).Count() <= MaxTargets)
            .WithMessage($"At most {MaxTargets} targets are allowed.");

        RuleForEach(x => x.Targets).SetValidator(new WindowsWirelessSetupTargetRequestValidator());

        RuleFor(x => x.Settings)
            .Custom((settings, context) =>
            {
                var bulk = (WindowsWirelessSetupExecuteNowBulkRequest)context.InstanceToValidate!;
                var uniqueCount = bulk.Targets
                    .GroupBy(t => t.MacAddress.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Count();
                var validator = new WindowsWirelessSetupSettingsRequestValidator(requireManualIpAddress: uniqueCount <= 1);
                var result = validator.Validate(settings);
                foreach (var error in result.Errors)
                {
                    context.AddFailure(error);
                }
            });

        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");

        RuleFor(x => x)
            .Must(x => WindowsWirelessSetupSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                x.Targets.FirstOrDefault()?.MacAddress ?? WindowsWirelessSetupTestValidationMacAddress.Value,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsWirelessSetupSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsWirelessSetupExecuteNowGroupRequestValidator : AbstractValidator<WindowsWirelessSetupExecuteNowGroupRequest>
{
    public WindowsWirelessSetupExecuteNowGroupRequestValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();

        RuleFor(x => x.Settings).SetValidator(new WindowsWirelessSetupSettingsRequestValidator(requireManualIpAddress: false));

        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");

        RuleFor(x => x)
            .Must(x => WindowsWirelessSetupSettingsRequestValidation.PayloadWithinLimit(
                x.Settings,
                WindowsWirelessSetupTestValidationMacAddress.Value,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsWirelessSetupSettingsRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

internal static class WindowsWirelessSetupTestValidationMacAddress
{
    public const string Value = "AA:BB:CC:DD:EE:10:XP";
}

public sealed class WindowsWirelessPropertiesExecuteNowRequestValidator : AbstractValidator<WindowsWirelessPropertiesExecuteNowRequest>
{
    public WindowsWirelessPropertiesExecuteNowRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsWirelessPropertiesTargetRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x.Operation)
            .Must(o => o is WirelessProfileOperation.Add or WirelessProfileOperation.Update)
            .WithMessage("operation must be Add or Update for execute-now profile apply.");

        When(x => x.Operation == WirelessProfileOperation.Add, () =>
        {
            RuleFor(x => x.Profile).SetValidator(new WindowsWirelessPropertiesProfileRequestValidator(requireFullProfile: true));
        });

        When(x => x.Operation == WirelessProfileOperation.Update, () =>
        {
            RuleFor(x => x.Profile).SetValidator(new WindowsWirelessPropertiesProfileRequestValidator(requireFullProfile: true));
        });
    }
}

public sealed class WindowsWirelessPropertiesQueueRequestValidator : AbstractValidator<WindowsWirelessPropertiesQueueRequest>
{
    public WindowsWirelessPropertiesQueueRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsWirelessPropertiesTargetRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "Queue", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be Queue for this endpoint.");
        RuleFor(x => x.Operation)
            .Must(o => o is WirelessProfileOperation.Add or WirelessProfileOperation.Update)
            .WithMessage("operation must be Add or Update for queue profile apply.");

        When(x => x.Operation == WirelessProfileOperation.Add, () =>
        {
            RuleFor(x => x.Profile).SetValidator(new WindowsWirelessPropertiesProfileRequestValidator(requireFullProfile: true));
        });

        When(x => x.Operation == WirelessProfileOperation.Update, () =>
        {
            RuleFor(x => x.Profile).SetValidator(new WindowsWirelessPropertiesProfileRequestValidator(requireFullProfile: true));
        });
    }
}

public sealed class WindowsWirelessPropertiesDeleteRequestValidator : AbstractValidator<WindowsWirelessPropertiesDeleteRequest>
{
    public WindowsWirelessPropertiesDeleteRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsWirelessPropertiesTargetRequestValidator());
        RuleFor(x => x.Ssid)
            .NotEmpty()
            .MaximumLength(128);
        RuleFor(x => x.Execution)
            .Must(e =>
                string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.ScheduleType?.Trim(), "Queue", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply or Queue.");
    }
}

public sealed class WindowsWirelessPropertiesTargetRequestValidator : AbstractValidator<WindowsWirelessPropertiesTargetRequest>
{
    public WindowsWirelessPropertiesTargetRequestValidator()
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

public sealed class WindowsWirelessPropertiesProfileRequestValidator : AbstractValidator<WindowsWirelessPropertiesProfileRequest>
{
    public const int MaxSsidLength = 128;
    public const int MaxNetworkNameLength = 50;
    public const int MaxKeyLength = 100;
    public const int MaxPreSharedKeyLength = 100;

    public WindowsWirelessPropertiesProfileRequestValidator(bool requireFullProfile = true)
    {
        RuleFor(x => x.Ssid)
            .NotEmpty()
            .MaximumLength(MaxSsidLength);

        if (!requireFullProfile)
        {
            return;
        }

        RuleFor(x => x.NetworkAuthentication)
            .NotEmpty()
            .Must(WindowsWirelessPropertiesProfileRequestValidation.IsValidAuthentication)
            .WithMessage("networkAuthentication must be a supported FusionX auth value.");

        RuleFor(x => x)
            .Must(WindowsWirelessPropertiesProfileRequestValidation.HasValidEncryptionForAuth)
            .WithMessage("dataEncryption must be None for Open auth and required otherwise.");

        RuleFor(x => x.NetworkKey)
            .Must((profile, key) => WindowsWirelessPropertiesProfileRequestValidation.IsValidNetworkKey(profile, key))
            .WithMessage("networkKey is required (8-100 chars, FusionX charset) for Shared and WPA-Personal profiles.");

        RuleFor(x => x.PreSharedKey)
            .MaximumLength(MaxPreSharedKeyLength);

        RuleFor(x => x.NetworkName)
            .MaximumLength(MaxNetworkNameLength);

        RuleFor(x => x.KeyIndex)
            .InclusiveBetween(0, 4)
            .Must((profile, index) => WindowsWirelessPropertiesProfileRequestValidation.IsValidKeyIndex(profile, index))
            .WithMessage("keyIndex must be between 1 and 4 when specified for keyed profiles.");

        RuleFor(x => x.Text2).MaximumLength(128);
        RuleFor(x => x.Text3).MaximumLength(128);
    }
}

internal static class WindowsWirelessPropertiesProfileRequestValidation
{
    private static readonly HashSet<string> ValidAuthenticationValues = new(StringComparer.Ordinal)
    {
        "No authentication (Open)",
        "Shared",
        "WPA-Enterprise",
        "WPA-Personal",
        "WPA2-Enterprise",
        "WPA2-Personal"
    };

    private static readonly System.Text.RegularExpressions.Regex NetworkKeyRegex =
        new("^[a-zA-Z0-9%!@#$%^&*()_+-=/~`]{8,}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static bool IsValidAuthentication(string? authentication) =>
        !string.IsNullOrWhiteSpace(authentication) && ValidAuthenticationValues.Contains(authentication.Trim());

    public static bool IsOpenAuth(string? authentication) =>
        string.Equals(authentication?.Trim(), "No authentication (Open)", StringComparison.Ordinal);

    public static bool RequiresNetworkKey(string? authentication)
    {
        if (string.IsNullOrWhiteSpace(authentication))
        {
            return false;
        }

        var auth = authentication.Trim();
        return auth == "Shared" ||
               auth.Contains("Personal", StringComparison.Ordinal);
    }

    public static bool HasValidEncryptionForAuth(WindowsWirelessPropertiesProfileRequest profile)
    {
        if (!IsValidAuthentication(profile.NetworkAuthentication))
        {
            return false;
        }

        if (IsOpenAuth(profile.NetworkAuthentication))
        {
            return string.IsNullOrWhiteSpace(profile.DataEncryption) ||
                   string.Equals(profile.DataEncryption.Trim(), "None", StringComparison.OrdinalIgnoreCase);
        }

        return !string.IsNullOrWhiteSpace(profile.DataEncryption) &&
               !string.Equals(profile.DataEncryption.Trim(), "None", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsValidNetworkKey(WindowsWirelessPropertiesProfileRequest profile, string? networkKey)
    {
        if (!RequiresNetworkKey(profile.NetworkAuthentication))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(networkKey))
        {
            return false;
        }

        var trimmed = networkKey.Trim();
        return trimmed.Length <= WindowsWirelessPropertiesProfileRequestValidator.MaxKeyLength &&
               NetworkKeyRegex.IsMatch(trimmed);
    }

    public static bool IsValidKeyIndex(WindowsWirelessPropertiesProfileRequest profile, int keyIndex)
    {
        if (!RequiresNetworkKey(profile.NetworkAuthentication))
        {
            return keyIndex == 0;
        }

        return keyIndex is >= 1 and <= 4;
    }
}

public sealed class WindowsWirelessPropertiesHistoryQueryValidator : AbstractValidator<WindowsWirelessPropertiesHistoryQuery>
{
    public WindowsWirelessPropertiesHistoryQueryValidator()
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

public sealed class WindowsWirelessPropertiesExecuteNowBulkRequestValidator : AbstractValidator<WindowsWirelessPropertiesExecuteNowBulkRequest>
{
    public const int MaxTargets = 500;

    public WindowsWirelessPropertiesExecuteNowBulkRequestValidator()
    {
        RuleFor(x => x.Targets)
            .NotEmpty()
            .WithMessage("At least one target is required.");

        RuleFor(x => x.Targets)
            .Must(targets => targets.GroupBy(t => t.MacAddress.Trim(), StringComparer.OrdinalIgnoreCase).Count() <= MaxTargets)
            .WithMessage($"At most {MaxTargets} targets are allowed.");

        RuleForEach(x => x.Targets).SetValidator(new WindowsWirelessPropertiesTargetRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x.Operation)
            .Must(o => o is WirelessProfileOperation.Add or WirelessProfileOperation.Update)
            .WithMessage("operation must be Add or Update for execute-now bulk.");
        RuleFor(x => x.Profile).SetValidator(new WindowsWirelessPropertiesProfileRequestValidator(requireFullProfile: true));
    }
}

public sealed class WindowsWirelessPropertiesExecuteNowGroupRequestValidator : AbstractValidator<WindowsWirelessPropertiesExecuteNowGroupRequest>
{
    public WindowsWirelessPropertiesExecuteNowGroupRequestValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x.Operation)
            .Must(o => o is WirelessProfileOperation.Add or WirelessProfileOperation.Update)
            .WithMessage("operation must be Add or Update for execute-now group.");
        RuleFor(x => x.Profile).SetValidator(new WindowsWirelessPropertiesProfileRequestValidator(requireFullProfile: true));
    }
}

public sealed class WindowsWirelessPropertiesDeleteExecuteNowBulkRequestValidator : AbstractValidator<WindowsWirelessPropertiesDeleteExecuteNowBulkRequest>
{
    public const int MaxTargets = 500;

    public WindowsWirelessPropertiesDeleteExecuteNowBulkRequestValidator()
    {
        RuleFor(x => x.Targets)
            .NotEmpty()
            .WithMessage("At least one target is required.");

        RuleFor(x => x.Targets)
            .Must(targets => targets.GroupBy(t => t.MacAddress.Trim(), StringComparer.OrdinalIgnoreCase).Count() <= MaxTargets)
            .WithMessage($"At most {MaxTargets} targets are allowed.");

        RuleForEach(x => x.Targets).SetValidator(new WindowsWirelessPropertiesTargetRequestValidator());
        RuleFor(x => x.Ssid)
            .NotEmpty()
            .MaximumLength(WindowsWirelessPropertiesProfileRequestValidator.MaxSsidLength);
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
    }
}

public sealed class WindowsWirelessPropertiesDeleteExecuteNowGroupRequestValidator : AbstractValidator<WindowsWirelessPropertiesDeleteExecuteNowGroupRequest>
{
    public WindowsWirelessPropertiesDeleteExecuteNowGroupRequestValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Ssid)
            .NotEmpty()
            .MaximumLength(WindowsWirelessPropertiesProfileRequestValidator.MaxSsidLength);
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
    }
}