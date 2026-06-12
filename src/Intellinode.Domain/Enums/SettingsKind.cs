namespace Intellinode.Domain.Enums;

public enum SettingsKind
{
    General,
    Advanced,
    /// <summary>FusionX ModuleType "Keyboard".</summary>
    Keyboard,
    /// <summary>FusionX ModuleType "Mouse".</summary>
    Mouse,
    /// <summary>FusionX ModuleType "Display".</summary>
    Display,
    /// <summary>FusionX module "Windows_802_1x".</summary>
    Windows8021x,
    /// <summary>FusionX Computer Name / Domain Join (Host Name, DomainSettings modules).</summary>
    WindowsComputerName,
    /// <summary>FusionX module "Ethernet".</summary>
    WindowsEthernetSetup,
    /// <summary>FusionX module "Wireless" (Network Settings → Wireless Setup / Wi‑Fi IP).</summary>
    WindowsWirelessSetup,
    /// <summary>FusionX module "Wireless Network Security" (Network Settings → Wireless Properties).</summary>
    WindowsWirelessProperties,
    /// <summary>FusionX modules DateTime / TimeZone / TimeServerSynchro (System Settings → Time and Language).</summary>
    WindowsDateTimeSetup,
    /// <summary>FusionX "Region And Location Settings" (System Settings → Time and Language).</summary>
    WindowsRegionLocation,
    /// <summary>FusionX "Regional Settings" (System Settings → Time and Language).</summary>
    WindowsRegionalFormat
}
