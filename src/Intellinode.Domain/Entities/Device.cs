using Intellinode.Domain.Enums;

namespace Intellinode.Domain.Entities;

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string MacAddress { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string CommunicationIpAddress { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string PrimaryDns { get; set; } = string.Empty;
    public string SecondaryDns { get; set; } = string.Empty;
    public string PrimaryWins { get; set; } = string.Empty;
    public string SecondaryWins { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Workgroup { get; set; } = string.Empty;
    public string LoginUserName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string LicenseKey { get; set; } = string.Empty;
    public string CommunicationType { get; set; } = string.Empty;
    public string AgentUpTime { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public int PollInterval { get; set; }
    public bool IsDhcp { get; set; }
    public bool IsDomainJoined { get; set; }
    public bool IsOnline { get; set; }
    public bool IsServiceMode { get; set; }
    public bool IsLicensed { get; set; } = true;
    public bool IsRegistered { get; set; }
    public EnrollmentState EnrollmentState { get; set; } = EnrollmentState.PendingInventory;
    public string ClientStatus { get; set; } = "OFF";
    public string Os { get; set; } = "Windows";
    public string OsVersion { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public DateTime? LastHeartbeatUtc { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public Guid? GroupId { get; set; }
    public DeviceGroup? Group { get; set; }
    public ICollection<HeartbeatBindingChange> BindingChanges { get; set; } = [];
    public ICollection<DeviceTask> Tasks { get; set; } = [];
    public ICollection<AgentRefreshToken> RefreshTokens { get; set; } = [];
    public DeviceInventory? Inventory { get; set; }
    public DeviceRemoteSettings? RemoteSettings { get; set; }
    public DeviceKeyboardSettings? KeyboardSettings { get; set; }
    public DeviceMouseSettings? MouseSettings { get; set; }
    public DeviceDisplaySettings? DisplaySettings { get; set; }
    public DeviceWindows8021xSettings? Windows8021xSettings { get; set; }
    public DeviceWindowsComputerNameSettings? WindowsComputerNameSettings { get; set; }
    public DeviceWindowsDateTimeSettings? WindowsDateTimeSettings { get; set; }
    public DeviceWindowsRegionLocationSettings? WindowsRegionLocationSettings { get; set; }
    public DeviceWindowsRegionalFormatSettings? WindowsRegionalFormatSettings { get; set; }
    public DeviceWindowsEthernetSettings? WindowsEthernetSetupSettings { get; set; }
    public DeviceWindowsWirelessSetupSettings? WindowsWirelessSetupSettings { get; set; }
    public ICollection<DeviceWindows8021xSettingsSnapshot> Windows8021xSnapshots { get; set; } = [];
    public ICollection<DeviceWindowsWirelessProfileSettings> WindowsWirelessProfiles { get; set; } = [];
    public ICollection<DeviceWindowsWirelessProfileSettingsSnapshot> WindowsWirelessProfileSnapshots { get; set; } = [];
    public DeviceWindowsPowerManagementSettings? WindowsPowerManagementSettings { get; set; }
    public ICollection<DeviceWindowsPowerManagementSettingsSnapshot> WindowsPowerManagementSnapshots { get; set; } = [];
    public DeviceWindowsScreenSaverSettings? WindowsScreenSaverSettings { get; set; }
    public ICollection<DeviceWindowsScreenSaverSettingsSnapshot> WindowsScreenSaverSnapshots { get; set; } = [];
    public DeviceWindowsTaskbarSettings? WindowsTaskbarSettings { get; set; }
    public DeviceWindowsTaskbarLiveSettings? WindowsTaskbarLiveSettings { get; set; }
    public DeviceAgentAdvancedSettings? AgentAdvancedSettings { get; set; }
}
