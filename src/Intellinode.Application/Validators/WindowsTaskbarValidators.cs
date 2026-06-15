using FluentValidation;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Validation;

namespace Intellinode.Application.Validators;

public sealed class WindowsTaskbarTemplateQueueRequestValidator : AbstractValidator<WindowsTaskbarTemplateQueueRequest>
{
    public WindowsTaskbarTemplateQueueRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsTaskbarTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsTaskbarSettingsRequestValidator());
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
            .Must(x => WindowsTaskbarRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsTaskbarRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsTaskbarExecuteNowBulkRequestValidator : AbstractValidator<WindowsTaskbarExecuteNowBulkRequest>
{
    public const int MaxTargets = 500;

    public WindowsTaskbarExecuteNowBulkRequestValidator()
    {
        RuleFor(x => x.Targets)
            .NotEmpty()
            .WithMessage("At least one target is required.");
        RuleFor(x => x.Targets)
            .Must(targets => targets.Count <= MaxTargets)
            .WithMessage($"At most {MaxTargets} targets are allowed.");
        RuleForEach(x => x.Targets).SetValidator(new WindowsTaskbarTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsTaskbarSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsTaskbarRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsTaskbarRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsTaskbarExecuteNowGroupRequestValidator : AbstractValidator<WindowsTaskbarExecuteNowGroupRequest>
{
    public WindowsTaskbarExecuteNowGroupRequestValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Settings).SetValidator(new WindowsTaskbarSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsTaskbarRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsTaskbarRequestValidation.MaxFunctionParameterLength} characters.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class AgentTaskbarLiveReportRequestValidator : AbstractValidator<AgentTaskbarLiveReportRequest>
{
    public AgentTaskbarLiveReportRequestValidator()
    {
        RuleFor(x => x)
            .Must(HasAnySettings)
            .WithMessage("At least one taskbar setting must be provided (flat fields or WinCELinux.XPTaskbarProperties).");

        RuleFor(x => x.LegacyTaskId)
            .GreaterThan(0)
            .When(x => x.LegacyTaskId.HasValue);
    }

    private static bool HasAnySettings(AgentTaskbarLiveReportRequest request) =>
        request.LockTaskbar.HasValue ||
        request.AutoHideTaskbar.HasValue ||
        request.KeepTaskbarOnTop.HasValue ||
        request.GroupSimilarButtons.HasValue ||
        request.ShowQuickLaunch.HasValue ||
        request.ShowClock.HasValue ||
        request.HideInactiveIcons.HasValue ||
        request.WinCELinux.HasValue;
}
