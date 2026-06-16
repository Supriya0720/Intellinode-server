namespace Intellinode.Domain.Entities;

/// <summary>
/// Desired/applied Windows application/command settings per device (FusionX Administration → Application command).
/// </summary>
public sealed class DeviceWindowsApplicationCommandSettings
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    /// <summary><c>Application</c> or <c>Command</c> (FusionX <c>IsApplicationOrCommand</c> / ModuleType).</summary>
    public string Mode { get; set; } = "Application";
    public string ApplicationPath { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
    public bool WarnUser { get; set; }
    public string AlertTitle { get; set; } = string.Empty;
    public string AlertMessage { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string DisplayTime { get; set; } = string.Empty;
    public string CommandText { get; set; } = string.Empty;
    public string Timeout { get; set; } = string.Empty;
    public bool RebootRequired { get; set; }
    public bool RequireCommandOutput { get; set; }
    public int AgentAction { get; set; }
    public long SettingsVersion { get; set; } = 1;
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
