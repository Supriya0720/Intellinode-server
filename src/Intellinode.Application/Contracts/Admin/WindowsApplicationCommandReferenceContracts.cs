namespace Intellinode.Application.Contracts.Admin;

/// <summary>
/// FusionX <c>Windows_ucAplicationAndCommand</c> dropdown reference item (value sent to agent + display label).
/// </summary>
public sealed class WindowsApplicationCommandReferenceItemDto
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class WindowsApplicationCommandReferenceOptionsResponse
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public WindowsApplicationCommandReferenceOptionsData Data { get; set; } = new();
}

public sealed class WindowsApplicationCommandReferenceOptionsData
{
    public List<WindowsApplicationCommandReferenceItemDto> MessageTypes { get; set; } = [];
    public List<WindowsApplicationCommandReferenceItemDto> DisplayTimes { get; set; } = [];
    public List<WindowsApplicationCommandReferenceItemDto> Timeouts { get; set; } = [];
}

public sealed class WindowsApplicationCommandReferenceErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}
