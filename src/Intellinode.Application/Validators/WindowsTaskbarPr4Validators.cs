using FluentValidation;
using Intellinode.Application.Contracts.Agents;

namespace Intellinode.Application.Validators;

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
