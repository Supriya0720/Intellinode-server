using Intellinode.Domain.Enums;

namespace Intellinode.Application.Contracts.Admin;

public sealed class WindowsPowerPlanMasterDto
{
    public int Id { get; init; }
    public string PlanName { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
}

public sealed class WindowsPowerTimeoutMasterDto
{
    public int Id { get; init; }
    public string DisplayText { get; init; } = string.Empty;
    public int? ValueSeconds { get; init; }
    public WindowsPowerTimeoutCategory Category { get; init; }
}

public sealed class WindowsPowerAdvancedOptionValueDto
{
    public int Id { get; init; }
    public string DisplayText { get; init; } = string.Empty;
    public string ValueText { get; init; } = string.Empty;
}

public sealed class WindowsPowerAdvancedOptionSettingCatalogDto
{
    public string SettingName { get; init; } = string.Empty;
    public List<WindowsPowerAdvancedOptionValueDto> Values { get; init; } = [];
}

public sealed class WindowsPowerAdvancedOptionGroupCatalogDto
{
    public string OptionName { get; init; } = string.Empty;
    public string? PlanName { get; init; }
    public List<WindowsPowerAdvancedOptionSettingCatalogDto> Settings { get; init; } = [];
}

public sealed class PowerManagementReferenceErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class PowerManagementReferenceResult<T>
{
    public TimeAndLanguageReferenceListResponse<T>? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static PowerManagementReferenceResult<T> Success(TimeAndLanguageReferenceListResponse<T> response) =>
        new() { Response = response };

    public static PowerManagementReferenceResult<T> Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}
