using FluentValidation;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Validation;

namespace Intellinode.Application.Validators;

public sealed class WindowsUserInterfaceExecuteNowRequestValidator : AbstractValidator<WindowsUserInterfaceExecuteNowRequest>
{
    public WindowsUserInterfaceExecuteNowRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsUserInterfaceTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsUserInterfaceSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsUserInterfaceRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction),
                useCompactTaskReference: false))
            .WithMessage($"Serialized agent payload exceeds {WindowsUserInterfaceRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsUserInterfaceQueueRequestValidator : AbstractValidator<WindowsUserInterfaceQueueRequest>
{
    public WindowsUserInterfaceQueueRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsUserInterfaceTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsUserInterfaceSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "Queue", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be Queue for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsUserInterfaceRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction),
                useCompactTaskReference: true))
            .WithMessage($"Serialized agent payload exceeds {WindowsUserInterfaceRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsUserInterfaceTargetRequestValidator : AbstractValidator<WindowsUserInterfaceTargetRequest>
{
    public WindowsUserInterfaceTargetRequestValidator()
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

public sealed class WindowsUserInterfaceSettingsRequestValidator : AbstractValidator<WindowsUserInterfaceSettingsRequest>
{
    public WindowsUserInterfaceSettingsRequestValidator()
    {
        RuleFor(x => x.UserName)
            .MaximumLength(WindowsUserInterfaceModuleConstants.MaxUserNameLength);

        RuleFor(x => x.Password)
            .MaximumLength(WindowsUserInterfaceModuleConstants.MaxPasswordLength)
            .When(x => !string.IsNullOrEmpty(x.Password));

        RuleFor(x => x)
            .Must(x => WindowsUserInterfaceRequestValidation.ValidateAutologonCredentials(x) is null)
            .WithMessage(x => WindowsUserInterfaceRequestValidation.ValidateAutologonCredentials(x)!);
    }
}

public sealed class WindowsUserInterfaceHistoryQueryValidator : AbstractValidator<WindowsUserInterfaceHistoryQuery>
{
    public WindowsUserInterfaceHistoryQueryValidator()
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

public sealed class WindowsUserInterfaceTemplateQueueRequestValidator : AbstractValidator<WindowsUserInterfaceTemplateQueueRequest>
{
    public WindowsUserInterfaceTemplateQueueRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsUserInterfaceTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsUserInterfaceSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "QueueTemplate", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be QueueTemplate for this endpoint.");
        RuleFor(x => x.Execution.TemplateId)
            .NotNull()
            .GreaterThan(0)
            .WithMessage("templateId must be greater than 0.");
        RuleFor(x => x.Execution.TemplateName)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("templateName is required.");
        RuleFor(x => x)
            .Must(x => WindowsUserInterfaceRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction),
                useCompactTaskReference: true))
            .WithMessage($"Serialized agent payload exceeds {WindowsUserInterfaceRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsUserInterfaceExecuteNowBulkRequestValidator : AbstractValidator<WindowsUserInterfaceExecuteNowBulkRequest>
{
    public const int MaxTargets = 500;

    public WindowsUserInterfaceExecuteNowBulkRequestValidator()
    {
        RuleFor(x => x.Targets)
            .NotEmpty()
            .WithMessage("At least one target is required.");
        RuleFor(x => x.Targets)
            .Must(targets => targets.Count <= MaxTargets)
            .WithMessage($"At most {MaxTargets} targets are allowed.");
        RuleForEach(x => x.Targets).SetValidator(new WindowsUserInterfaceTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsUserInterfaceSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsUserInterfaceRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction),
                useCompactTaskReference: false))
            .WithMessage($"Serialized agent payload exceeds {WindowsUserInterfaceRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsUserInterfaceExecuteNowGroupRequestValidator : AbstractValidator<WindowsUserInterfaceExecuteNowGroupRequest>
{
    public WindowsUserInterfaceExecuteNowGroupRequestValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Settings).SetValidator(new WindowsUserInterfaceSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsUserInterfaceRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction),
                useCompactTaskReference: false))
            .WithMessage($"Serialized agent payload exceeds {WindowsUserInterfaceRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}
