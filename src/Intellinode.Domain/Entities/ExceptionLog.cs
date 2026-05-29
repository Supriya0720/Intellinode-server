namespace Intellinode.Domain.Entities;

public sealed class ExceptionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? RequestPath { get; set; }
    public string? HttpMethod { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid? AdminId { get; set; }
    public DateTime LoggedUtc { get; set; } = DateTime.UtcNow;
}
