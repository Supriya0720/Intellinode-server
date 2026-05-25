using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;

namespace Intellinode.Infrastructure.Services;

internal static class DeviceTaskOperations
{
    public static void SetCompletion(DeviceTask task, DeviceTaskStatus status)
    {
        task.Status = status;
        task.CompletedUtc = DateTime.UtcNow;
    }

    public static void ApplyDeviceStateAfterCompletion(
        Device device,
        DeviceTask task,
        DeviceTaskStatus status,
        string? clientStatus = null)
    {
        if (status != DeviceTaskStatus.Completed)
        {
            return;
        }

        if (task.ModuleName == "Wake On Lan")
        {
            device.IsOnline = true;
            device.ClientStatus = ClientPowerStatus.On;
            return;
        }

        if (task.FunctionName == "Shutdown")
        {
            device.IsOnline = false;
            device.ClientStatus = ClientPowerStatus.Off;
            return;
        }

        if (task.FunctionName == "Restart" && clientStatus is not null)
        {
            device.IsOnline = clientStatus == ClientPowerStatus.On;
            device.ClientStatus = clientStatus;
        }
    }

    public static string ExtractSignal(string? extraData)
    {
        if (string.IsNullOrWhiteSpace(extraData))
        {
            return string.Empty;
        }

        var trimmed = extraData.Trim();
        if (trimmed.StartsWith('&'))
        {
            return trimmed;
        }

        if (trimmed.Length <= 32 &&
            !trimmed.Contains(' ') &&
            trimmed.All(c => char.IsLetterOrDigit(c) || c is '&' or '_' or '-'))
        {
            return trimmed;
        }

        return string.Empty;
    }

    public static string ResolveExtraData(string? signal, string? extraData)
    {
        if (!string.IsNullOrWhiteSpace(signal))
        {
            return signal.Trim();
        }

        return extraData?.Trim() ?? string.Empty;
    }

    public static string MapStatusToString(DeviceTaskStatus status) =>
        status switch
        {
            DeviceTaskStatus.Pending => "Pending",
            DeviceTaskStatus.InProcess => "InProcess",
            DeviceTaskStatus.Completed => "Completed",
            DeviceTaskStatus.Failed => "Failed",
            _ => status.ToString()
        };

    public static DeviceTaskStatus ParseAckStatus(string status) =>
        status.Trim() switch
        {
            "Completed" => DeviceTaskStatus.Completed,
            "Failed" => DeviceTaskStatus.Failed,
            _ => throw new ArgumentException($"Unsupported task acknowledgement status '{status}'.")
        };
}
