using System.Text.Json;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain.Enums;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsComputerNamePayloadBuilder : IWindowsComputerNamePayloadBuilder
{
    public const int MaxFunctionParameterLength = 512;

    private static readonly string[] EmptyTextFields = ["", "", "", "", ""];

    public string BuildHostRenamePayload(WindowsComputerNameHostRenamePayloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = new
        {
            MacAddr = request.MacAddr,
            HostName = request.HostName,
            Domain = request.Domain,
            WorkGroup = request.WorkGroup,
            UserName = request.UserName,
            Password = request.Password,
            prefix = request.Prefix,
            postfix = request.Postfix,
            noOfChar = request.NoOfChar,
            IsMacOrSrNo = request.IsMacOrSrNo,
            Text1 = EmptyTextFields[0],
            Text2 = EmptyTextFields[1],
            Text3 = EmptyTextFields[2],
            Text4 = EmptyTextFields[3],
            Text5 = EmptyTextFields[4],
            TaskID = request.TaskID,
            AgentAction = request.AgentAction
        };

        return JsonSerializer.Serialize(new { WinCELinux = new { WindowsComputerNameSettings = settings } });
    }

    public string BuildDomainJoinPayload(WindowsComputerNameDomainJoinPayloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = new
        {
            MacAddr = request.MacAddr,
            IsDomainWorkgroup = request.IsDomainJoin ? "True" : "False",
            HostName = request.HostName,
            Domain = request.Domain,
            WorkGroup = request.WorkGroup,
            UserName = request.UserName,
            Password = request.Password,
            OrganizationalUnit = request.OrganizationalUnit,
            Text1 = EmptyTextFields[0],
            Text2 = EmptyTextFields[1],
            Text3 = EmptyTextFields[2],
            Text4 = EmptyTextFields[3],
            Text5 = EmptyTextFields[4],
            TaskID = request.TaskID,
            AgentAction = request.AgentAction
        };

        return JsonSerializer.Serialize(new { WinCELinux = new { WindowsDomainSettings = settings } });
    }

    public string GetModuleNameForApplyMode(ComputerNameApplyMode mode) =>
        mode switch
        {
            ComputerNameApplyMode.HostRename => WindowsComputerNameModuleConstants.HostRenameModuleName,
            ComputerNameApplyMode.DomainJoin => WindowsComputerNameModuleConstants.DomainJoinModuleName,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported computer name apply mode.")
        };

    public static string MapEntityToMacAddr(string deviceMacAddress) =>
        deviceMacAddress.Trim().EndsWith(":XP", StringComparison.OrdinalIgnoreCase)
            ? deviceMacAddress.Trim()
            : $"{deviceMacAddress.Trim()}:XP";
}
