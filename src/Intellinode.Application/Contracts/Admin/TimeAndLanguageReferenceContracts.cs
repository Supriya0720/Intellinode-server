namespace Intellinode.Application.Contracts.Admin;

public sealed class RegionLocationMasterDto
{
    public int Id { get; init; }
    public char Identifier { get; init; }
    public string Value { get; init; } = string.Empty;
    public string? Bcp47Code { get; init; }
}

public sealed class WindowsTimeZoneMasterDto
{
    public int Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string WindowsTzKey { get; init; } = string.Empty;
}

public sealed class TimeAndLanguageReferenceListResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<T> Data { get; set; } = [];
}

public sealed class TimeAndLanguageReferenceResult<T>
{
    public TimeAndLanguageReferenceListResponse<T>? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static TimeAndLanguageReferenceResult<T> Success(TimeAndLanguageReferenceListResponse<T> response) =>
        new() { Response = response };

    public static TimeAndLanguageReferenceResult<T> Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class TimeAndLanguageReferenceErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}
