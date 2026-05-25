namespace Intellinode.Domain.Enums;

public static class ClientPowerStatus
{
    public const string On = "ON";
    public const string Off = "OFF";
    public const string Coff = "COFF";
}

public static class ShutdownAcknowledgement
{
    public const string Shutdown = "SH";
    public const string ShutdownAck = "SHACK";
    public const string Restart = "RT";
    public const string RestartAck = "RTACK";
}

public static class ClientUpdateStatus
{
    public const string NoChange = "NC";
    public const string Changed = "C";
}
