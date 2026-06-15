namespace Intellinode.Domain.Entities;

/// <summary>
/// Agent-reported live taskbar state (FusionX <c>XPTaskbar_Details</c> / <c>Input_prcGetXPTaskbarProperties</c>).
/// Separate from desired/applied settings in <see cref="DeviceWindowsTaskbarSettings"/>.
/// </summary>
public sealed class DeviceWindowsTaskbarLiveSettings
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public bool LockTaskbar { get; set; } = true;
    public bool AutoHideTaskbar { get; set; }
    public bool KeepTaskbarOnTop { get; set; } = true;
    public bool GroupSimilarButtons { get; set; } = true;
    public bool ShowQuickLaunch { get; set; }
    public bool ShowClock { get; set; }
    public bool HideInactiveIcons { get; set; }
    public DateTime CollectedUtc { get; set; } = DateTime.UtcNow;
    public long ReportVersion { get; set; } = 1;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
