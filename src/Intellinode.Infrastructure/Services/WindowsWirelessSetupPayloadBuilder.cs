using System.Text.Json;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsWirelessSetupPayloadBuilder : IWindowsWirelessSetupPayloadBuilder
{
    public const int MaxFunctionParameterLength = 512;

    public string BuildWirelessPayload(WindowsWirelessSetupPayloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = new
        {
            MacAddr = request.MacAddr,
            DHCP = request.Dhcp,
            IPAddr = request.Dhcp ? string.Empty : request.IpAddr,
            SubnetMask = request.Dhcp ? string.Empty : request.SubnetMask,
            Gateway = request.Dhcp ? string.Empty : request.Gateway,
            PriDNS = request.Dhcp ? string.Empty : request.PriDns,
            SecDNS = request.Dhcp ? string.Empty : request.SecDns,
            PriWNS = request.Dhcp ? string.Empty : request.PriWns,
            SecWNS = request.Dhcp ? string.Empty : request.SecWns,
            networkType = "Wireless",
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
