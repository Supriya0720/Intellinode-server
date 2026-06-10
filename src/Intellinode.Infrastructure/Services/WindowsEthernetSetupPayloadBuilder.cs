using System.Text.Json;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsEthernetSetupPayloadBuilder : IWindowsEthernetSetupPayloadBuilder
{
    public const int MaxFunctionParameterLength = 512;

    public string BuildEthernetPayload(WindowsEthernetSetupPayloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var priDns = request.IsObtainedDnsAutomatically ? string.Empty : request.PriDns;
        var secDns = request.IsObtainedDnsAutomatically ? string.Empty : request.SecDns;

        var settings = new
        {
            MacAddr = request.MacAddr,
            DHCP = request.Dhcp,
            IPAddr = request.IpAddr,
            SubnetMask = request.SubnetMask,
            Gateway = request.Gateway,
            PriDNS = priDns,
            SecDNS = secDns,
            PriWNS = request.PriWns,
            SecWNS = request.SecWns,
            networkSpeed = request.NetworkSpeed,
            networkType = "Ethernet",
            IsObtainedDNSAutomatically = request.IsObtainedDnsAutomatically,
            TaskID = request.TaskID,
            AgentAction = request.AgentAction
        };

        return JsonSerializer.Serialize(new { WinCELinux = new { XPNetwork_Settings = settings } });
    }

    public static string MapEntityToMacAddr(string deviceMacAddress) =>
        deviceMacAddress.Trim().EndsWith(":XP", StringComparison.OrdinalIgnoreCase)
            ? deviceMacAddress.Trim()
            : $"{deviceMacAddress.Trim()}:XP";
}
