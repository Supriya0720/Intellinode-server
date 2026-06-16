using FluentValidation;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Validation;
using Microsoft.Extensions.Options;

namespace Intellinode.Application.Validators;

public sealed class WindowsApplicationCommandExecuteNowRequestValidator : AbstractValidator<WindowsApplicationCommandExecuteNowRequest>
{
    public WindowsApplicationCommandExecuteNowRequestValidator(
        IOptions<WindowsApplicationCommandValidationPolicy> validationPolicy)
    {
        var policy = validationPolicy.Value;

        RuleFor(x => x.Target).SetValidator(new WindowsApplicationCommandTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsApplicationCommandSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsApplicationCommandRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsApplicationCommandRequestValidation.MaxFunctionParameterLength} characters.");
        RuleFor(x => x.Settings)
            .Must(settings => WindowsApplicationCommandRequestValidation.ValidateSettings(settings, policy) is null)
            .WithMessage(x => WindowsApplicationCommandRequestValidation.ValidateSettings(x.Settings, policy)
                             ?? "Invalid application command settings.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsApplicationCommandQueueRequestValidator : AbstractValidator<WindowsApplicationCommandQueueRequest>
{
    public WindowsApplicationCommandQueueRequestValidator(
        IOptions<WindowsApplicationCommandValidationPolicy> validationPolicy)
    {
        var policy = validationPolicy.Value;

        RuleFor(x => x.Target).SetValidator(new WindowsApplicationCommandTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsApplicationCommandSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "Queue", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be Queue for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsApplicationCommandRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsApplicationCommandRequestValidation.MaxFunctionParameterLength} characters.");
        RuleFor(x => x.Settings)
            .Must(settings => WindowsApplicationCommandRequestValidation.ValidateSettings(settings, policy) is null)
            .WithMessage(x => WindowsApplicationCommandRequestValidation.ValidateSettings(x.Settings, policy)
                             ?? "Invalid application command settings.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsApplicationCommandTargetRequestValidator : AbstractValidator<WindowsApplicationCommandTargetRequest>
{
    public WindowsApplicationCommandTargetRequestValidator()
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

public sealed class WindowsApplicationCommandSettingsRequestValidator : AbstractValidator<WindowsApplicationCommandSettingsRequest>
{
    public WindowsApplicationCommandSettingsRequestValidator()
    {
        RuleFor(x => x.Mode)
            .NotEmpty()
            .Must(m => string.Equals(m.Trim(), WindowsApplicationCommandModuleConstants.ApplicationModuleName, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(m.Trim(), WindowsApplicationCommandModuleConstants.CommandModuleName, StringComparison.OrdinalIgnoreCase))
            .WithMessage("mode must be Application or Command.");

        RuleFor(x => x.ApplicationPath)
            .MaximumLength(WindowsApplicationCommandModuleConstants.MaxApplicationPathLength);

        RuleFor(x => x.Parameters)
            .MaximumLength(WindowsApplicationCommandModuleConstants.MaxParametersLength);

        RuleFor(x => x.AlertTitle)
            .MaximumLength(WindowsApplicationCommandModuleConstants.MaxAlertTitleLength);

        RuleFor(x => x.AlertMessage)
            .MaximumLength(WindowsApplicationCommandModuleConstants.MaxAlertMessageLength);

        RuleFor(x => x.MessageType)
            .MaximumLength(WindowsApplicationCommandModuleConstants.MaxMessageTypeLength);

        RuleFor(x => x.DisplayTime)
            .MaximumLength(WindowsApplicationCommandModuleConstants.MaxDisplayTimeLength);

        RuleFor(x => x.CommandText)
            .MaximumLength(WindowsApplicationCommandModuleConstants.MaxCommandTextLength);

        RuleFor(x => x.Timeout)
            .MaximumLength(WindowsApplicationCommandModuleConstants.MaxTimeoutLength);
    }
}

public sealed class WindowsApplicationCommandHistoryQueryValidator : AbstractValidator<WindowsApplicationCommandHistoryQuery>
{
    public WindowsApplicationCommandHistoryQueryValidator()
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

public sealed class WindowsApplicationCommandTemplateQueueRequestValidator : AbstractValidator<WindowsApplicationCommandTemplateQueueRequest>
{
    public WindowsApplicationCommandTemplateQueueRequestValidator(
        IOptions<WindowsApplicationCommandValidationPolicy> validationPolicy)
    {
        var policy = validationPolicy.Value;

        RuleFor(x => x.Target).SetValidator(new WindowsApplicationCommandTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsApplicationCommandSettingsRequestValidator());
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
            .Must(x => WindowsApplicationCommandRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsApplicationCommandRequestValidation.MaxFunctionParameterLength} characters.");
        RuleFor(x => x.Settings)
            .Must(settings => WindowsApplicationCommandRequestValidation.ValidateSettings(settings, policy) is null)
            .WithMessage(x => WindowsApplicationCommandRequestValidation.ValidateSettings(x.Settings, policy)
                             ?? "Invalid application command settings.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsApplicationCommandExecuteNowBulkRequestValidator : AbstractValidator<WindowsApplicationCommandExecuteNowBulkRequest>
{
    public const int MaxTargets = 500;

    public WindowsApplicationCommandExecuteNowBulkRequestValidator(
        IOptions<WindowsApplicationCommandValidationPolicy> validationPolicy)
    {
        var policy = validationPolicy.Value;

        RuleFor(x => x.Targets)
            .NotEmpty()
            .WithMessage("At least one target is required.");
        RuleFor(x => x.Targets)
            .Must(targets => targets.Count <= MaxTargets)
            .WithMessage($"At most {MaxTargets} targets are allowed.");
        RuleForEach(x => x.Targets).SetValidator(new WindowsApplicationCommandTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsApplicationCommandSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsApplicationCommandRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsApplicationCommandRequestValidation.MaxFunctionParameterLength} characters.");
        RuleFor(x => x.Settings)
            .Must(settings => WindowsApplicationCommandRequestValidation.ValidateSettings(settings, policy) is null)
            .WithMessage(x => WindowsApplicationCommandRequestValidation.ValidateSettings(x.Settings, policy)
                             ?? "Invalid application command settings.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsApplicationCommandExecuteNowGroupRequestValidator : AbstractValidator<WindowsApplicationCommandExecuteNowGroupRequest>
{
    public WindowsApplicationCommandExecuteNowGroupRequestValidator(
        IOptions<WindowsApplicationCommandValidationPolicy> validationPolicy)
    {
        var policy = validationPolicy.Value;

        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Settings).SetValidator(new WindowsApplicationCommandSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsApplicationCommandRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsApplicationCommandRequestValidation.MaxFunctionParameterLength} characters.");
        RuleFor(x => x.Settings)
            .Must(settings => WindowsApplicationCommandRequestValidation.ValidateSettings(settings, policy) is null)
            .WithMessage(x => WindowsApplicationCommandRequestValidation.ValidateSettings(x.Settings, policy)
                             ?? "Invalid application command settings.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}
