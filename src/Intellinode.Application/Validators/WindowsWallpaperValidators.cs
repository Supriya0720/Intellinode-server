using FluentValidation;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Validation;

namespace Intellinode.Application.Validators;

public sealed class WindowsWallpaperExecuteNowRequestValidator : AbstractValidator<WindowsWallpaperExecuteNowRequest>
{
    public WindowsWallpaperExecuteNowRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsWallpaperTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsWallpaperSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsWallpaperRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsWallpaperRequestValidation.MaxFunctionParameterLength} characters.");
        RuleFor(x => x.Settings)
            .Must(settings => WindowsWallpaperRequestValidation.ValidateRepositorySettings(settings) is null)
            .WithMessage(x => WindowsWallpaperRequestValidation.ValidateRepositorySettings(x.Settings) ?? "Invalid wallpaper settings.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsWallpaperQueueRequestValidator : AbstractValidator<WindowsWallpaperQueueRequest>
{
    public WindowsWallpaperQueueRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsWallpaperTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsWallpaperSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "Queue", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be Queue for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsWallpaperRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsWallpaperRequestValidation.MaxFunctionParameterLength} characters.");
        RuleFor(x => x.Settings)
            .Must(settings => WindowsWallpaperRequestValidation.ValidateRepositorySettings(settings) is null)
            .WithMessage(x => WindowsWallpaperRequestValidation.ValidateRepositorySettings(x.Settings) ?? "Invalid wallpaper settings.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsWallpaperTargetRequestValidator : AbstractValidator<WindowsWallpaperTargetRequest>
{
    public WindowsWallpaperTargetRequestValidator()
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

public sealed class WindowsWallpaperSettingsRequestValidator : AbstractValidator<WindowsWallpaperSettingsRequest>
{
    public WindowsWallpaperSettingsRequestValidator()
    {
        RuleFor(x => x.PicturePath)
            .MaximumLength(WindowsWallpaperModuleConstants.MaxPicturePathLength);

        RuleFor(x => x.PictureName)
            .MaximumLength(WindowsWallpaperModuleConstants.MaxPictureNameLength);

        RuleFor(x => x.PicturePosition)
            .NotEmpty()
            .Must(p => WindowsWallpaperModuleConstants.AllowedPicturePositions.Contains(
                p.Trim(),
                StringComparer.OrdinalIgnoreCase))
            .WithMessage("picturePosition must be one of Stretch, Tile, Center.");

        RuleFor(x => x.SourceType)
            .Must(s => string.IsNullOrWhiteSpace(s)
                       || WindowsWallpaperModuleConstants.AllowedSourceTypes.Contains(s.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("sourceType must be one of Browse, Upload, Repository.");
    }
}

public sealed class WindowsWallpaperHistoryQueryValidator : AbstractValidator<WindowsWallpaperHistoryQuery>
{
    public WindowsWallpaperHistoryQueryValidator()
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

public sealed class WindowsWallpaperTemplateQueueRequestValidator : AbstractValidator<WindowsWallpaperTemplateQueueRequest>
{
    public WindowsWallpaperTemplateQueueRequestValidator()
    {
        RuleFor(x => x.Target).SetValidator(new WindowsWallpaperTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsWallpaperSettingsRequestValidator());
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
            .Must(x => WindowsWallpaperRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsWallpaperRequestValidation.MaxFunctionParameterLength} characters.");
        RuleFor(x => x.Settings)
            .Must(settings => WindowsWallpaperRequestValidation.ValidateRepositorySettings(settings) is null)
            .WithMessage(x => WindowsWallpaperRequestValidation.ValidateRepositorySettings(x.Settings) ?? "Invalid wallpaper settings.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsWallpaperExecuteNowBulkRequestValidator : AbstractValidator<WindowsWallpaperExecuteNowBulkRequest>
{
    public const int MaxTargets = 500;

    public WindowsWallpaperExecuteNowBulkRequestValidator()
    {
        RuleFor(x => x.Targets)
            .NotEmpty()
            .WithMessage("At least one target is required.");
        RuleFor(x => x.Targets)
            .Must(targets => targets.Count <= MaxTargets)
            .WithMessage($"At most {MaxTargets} targets are allowed.");
        RuleForEach(x => x.Targets).SetValidator(new WindowsWallpaperTargetRequestValidator());
        RuleFor(x => x.Settings).SetValidator(new WindowsWallpaperSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsWallpaperRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsWallpaperRequestValidation.MaxFunctionParameterLength} characters.");
        RuleFor(x => x.Settings)
            .Must(settings => WindowsWallpaperRequestValidation.ValidateRepositorySettings(settings) is null)
            .WithMessage(x => WindowsWallpaperRequestValidation.ValidateRepositorySettings(x.Settings) ?? "Invalid wallpaper settings.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}

public sealed class WindowsWallpaperExecuteNowGroupRequestValidator : AbstractValidator<WindowsWallpaperExecuteNowGroupRequest>
{
    public WindowsWallpaperExecuteNowGroupRequestValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Settings).SetValidator(new WindowsWallpaperSettingsRequestValidator());
        RuleFor(x => x.Execution)
            .Must(e => string.Equals(e.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            .WithMessage("scheduleType must be InstantApply for this endpoint.");
        RuleFor(x => x)
            .Must(x => WindowsWallpaperRequestValidation.PayloadWithinLimit(
                x.Settings,
                ParseAgentAction(x.Execution.AgentAction)))
            .WithMessage($"Serialized agent payload exceeds {WindowsWallpaperRequestValidation.MaxFunctionParameterLength} characters.");
        RuleFor(x => x.Settings)
            .Must(settings => WindowsWallpaperRequestValidation.ValidateRepositorySettings(settings) is null)
            .WithMessage(x => WindowsWallpaperRequestValidation.ValidateRepositorySettings(x.Settings) ?? "Invalid wallpaper settings.");
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;
}
