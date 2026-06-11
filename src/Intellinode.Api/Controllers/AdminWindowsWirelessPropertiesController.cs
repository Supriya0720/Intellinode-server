using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentValidation;
using Intellinode.Api.Http;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Intellinode.Api.Controllers;

[ApiController]
[Route("api/v1/admin/device-config/windows-wireless-properties")]
[Authorize(Roles = "Admin")]
public sealed class AdminWindowsWirelessPropertiesController : ControllerBase
{
    private readonly IWindowsWirelessPropertiesSettingsService _wirelessPropertiesSettingsService;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<AdminWindowsWirelessPropertiesController> _logger;
    private readonly IValidator<WindowsWirelessPropertiesExecuteNowRequest> _executeNowValidator;
    private readonly IValidator<WindowsWirelessPropertiesQueueRequest> _queueValidator;
    private readonly IValidator<WindowsWirelessPropertiesDeleteRequest> _deleteValidator;
    private readonly IValidator<WindowsWirelessPropertiesHistoryQuery> _historyQueryValidator;
    private readonly IValidator<WindowsWirelessPropertiesExecuteNowBulkRequest> _executeNowBulkValidator;
    private readonly IValidator<WindowsWirelessPropertiesExecuteNowGroupRequest> _executeNowGroupValidator;
    private readonly IValidator<WindowsWirelessPropertiesDeleteExecuteNowBulkRequest> _deleteExecuteNowBulkValidator;
    private readonly IValidator<WindowsWirelessPropertiesDeleteExecuteNowGroupRequest> _deleteExecuteNowGroupValidator;
    private readonly WindowsWirelessPropertiesOptions _options;

    public AdminWindowsWirelessPropertiesController(
        IWindowsWirelessPropertiesSettingsService wirelessPropertiesSettingsService,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<AdminWindowsWirelessPropertiesController> logger,
        IValidator<WindowsWirelessPropertiesExecuteNowRequest> executeNowValidator,
        IValidator<WindowsWirelessPropertiesQueueRequest> queueValidator,
        IValidator<WindowsWirelessPropertiesDeleteRequest> deleteValidator,
        IValidator<WindowsWirelessPropertiesHistoryQuery> historyQueryValidator,
        IValidator<WindowsWirelessPropertiesExecuteNowBulkRequest> executeNowBulkValidator,
        IValidator<WindowsWirelessPropertiesExecuteNowGroupRequest> executeNowGroupValidator,
        IValidator<WindowsWirelessPropertiesDeleteExecuteNowBulkRequest> deleteExecuteNowBulkValidator,
        IValidator<WindowsWirelessPropertiesDeleteExecuteNowGroupRequest> deleteExecuteNowGroupValidator,
        IOptions<WindowsWirelessPropertiesOptions> options)
    {
        _wirelessPropertiesSettingsService = wirelessPropertiesSettingsService;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
        _executeNowValidator = executeNowValidator;
        _queueValidator = queueValidator;
        _deleteValidator = deleteValidator;
        _historyQueryValidator = historyQueryValidator;
        _executeNowBulkValidator = executeNowBulkValidator;
        _executeNowGroupValidator = executeNowGroupValidator;
        _deleteExecuteNowBulkValidator = deleteExecuteNowBulkValidator;
        _deleteExecuteNowGroupValidator = deleteExecuteNowGroupValidator;
        _options = options.Value;
    }

    [HttpGet("apply-history/{macAddress}")]
    public async Task<ActionResult<WindowsWirelessPropertiesHistoryResponse>> GetApplyHistory(
        string macAddress,
        [FromQuery] WindowsWirelessPropertiesHistoryQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();
            if (!_options.Enabled)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows Wireless Properties endpoint is disabled.", correlationId));
            }

            if (string.IsNullOrWhiteSpace(macAddress))
            {
                return BadRequest(BuildError("ValidationFailed", "macAddress is required.", correlationId));
            }

            var validation = await _historyQueryValidator.ValidateAsync(query, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError(
                    "ValidationFailed",
                    string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                    correlationId));
            }

            var result = await _wirelessPropertiesSettingsService.GetApplyHistoryAsync(macAddress.Trim(), query, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return MapReadError(result.ErrorCode, result.Message, correlationId);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(GetApplyHistory), ex, cancellationToken);
        }
    }

    [HttpGet("{macAddress}/profiles/{ssid}")]
    public async Task<ActionResult<WindowsWirelessPropertiesProfileResponse>> GetProfile(
        string macAddress,
        string ssid,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();
            if (!_options.Enabled)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows Wireless Properties endpoint is disabled.", correlationId));
            }

            if (string.IsNullOrWhiteSpace(macAddress))
            {
                return BadRequest(BuildError("ValidationFailed", "macAddress is required.", correlationId));
            }

            var result = await _wirelessPropertiesSettingsService.GetProfileAsync(macAddress.Trim(), ssid, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return MapReadError(result.ErrorCode, result.Message, correlationId);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(GetProfile), ex, cancellationToken);
        }
    }

    [HttpGet("{macAddress}")]
    public async Task<ActionResult<WindowsWirelessPropertiesListResponse>> ListProfiles(
        string macAddress,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();
            if (!_options.Enabled)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows Wireless Properties endpoint is disabled.", correlationId));
            }

            if (string.IsNullOrWhiteSpace(macAddress))
            {
                return BadRequest(BuildError("ValidationFailed", "macAddress is required.", correlationId));
            }

            var result = await _wirelessPropertiesSettingsService.ListProfilesAsync(macAddress.Trim(), cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return MapReadError(result.ErrorCode, result.Message, correlationId);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(ListProfiles), ex, cancellationToken);
        }
    }

    [HttpPost("queue")]
    public async Task<ActionResult<WindowsWirelessPropertiesQueueResponse>> Queue(
        [FromBody] WindowsWirelessPropertiesQueueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await MapWriteResultAsync(
                request.Options.CorrelationId,
                _options.Enabled && !_options.ReadOnly,
                await _queueValidator.ValidateAsync(request, cancellationToken),
                async () =>
                {
                    request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;
                    TryGetAdminId(out var adminId);
                    return await _wirelessPropertiesSettingsService.QueueAsync(request, adminId, cancellationToken);
                },
                success => success.Response,
                failure => failure.ErrorCode,
                failure => failure.Message);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(Queue), ex, cancellationToken);
        }
    }

    [HttpPost("execute-now")]
    public async Task<ActionResult<WindowsWirelessPropertiesExecuteNowResponse>> ExecuteNow(
        [FromBody] WindowsWirelessPropertiesExecuteNowRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await MapWriteResultAsync(
                request.Options.CorrelationId,
                _options.Enabled && !_options.ReadOnly,
                await _executeNowValidator.ValidateAsync(request, cancellationToken),
                async () =>
                {
                    request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;
                    TryGetAdminId(out var adminId);
                    return await _wirelessPropertiesSettingsService.ExecuteNowAsync(request, adminId, cancellationToken);
                },
                success => success.Response,
                failure => failure.ErrorCode,
                failure => failure.Message);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(ExecuteNow), ex, cancellationToken);
        }
    }

    [HttpPost("delete/execute-now")]
    public async Task<ActionResult<WindowsWirelessPropertiesDeleteExecuteNowResponse>> DeleteExecuteNow(
        [FromBody] WindowsWirelessPropertiesDeleteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows Wireless Properties endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _deleteValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError(
                    "ValidationFailed",
                    string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                    request.Options.CorrelationId));
            }

            if (!string.Equals(request.Execution.ScheduleType?.Trim(), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(BuildError(
                    "ValidationFailed",
                    "scheduleType must be InstantApply for delete execute-now.",
                    request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;
            TryGetAdminId(out var adminId);
            var result = await _wirelessPropertiesSettingsService.DeleteExecuteNowAsync(request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return MapWriteError(result.ErrorCode, result.Message, request.Options.CorrelationId);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(DeleteExecuteNow), ex, cancellationToken);
        }
    }

    [HttpPost("execute-now/bulk")]
    public async Task<ActionResult<WindowsWirelessPropertiesBulkResponse>> ExecuteNowBulk(
        [FromBody] WindowsWirelessPropertiesExecuteNowBulkRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows Wireless Properties endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _executeNowBulkValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError(
                    "ValidationFailed",
                    string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                    request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;
            TryGetAdminId(out var adminId);
            var result = await _wirelessPropertiesSettingsService.ExecuteNowBulkAsync(request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return MapBulkError(result.ErrorCode, result.Message, request.Options.CorrelationId);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(ExecuteNowBulk), ex, cancellationToken);
        }
    }

    [HttpPost("execute-now/group/{groupId:guid}")]
    public async Task<ActionResult<WindowsWirelessPropertiesBulkResponse>> ExecuteNowGroup(
        Guid groupId,
        [FromBody] WindowsWirelessPropertiesExecuteNowGroupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows Wireless Properties endpoint is disabled.", request.Options.CorrelationId));
            }

            request.GroupId = groupId;

            var validation = await _executeNowGroupValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError(
                    "ValidationFailed",
                    string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                    request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;
            TryGetAdminId(out var adminId);
            var result = await _wirelessPropertiesSettingsService.ExecuteNowGroupAsync(groupId, request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return MapBulkError(result.ErrorCode, result.Message, request.Options.CorrelationId);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(ExecuteNowGroup), ex, cancellationToken);
        }
    }

    [HttpPost("delete/execute-now/bulk")]
    public async Task<ActionResult<WindowsWirelessPropertiesBulkResponse>> DeleteExecuteNowBulk(
        [FromBody] WindowsWirelessPropertiesDeleteExecuteNowBulkRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows Wireless Properties endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _deleteExecuteNowBulkValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError(
                    "ValidationFailed",
                    string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                    request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;
            TryGetAdminId(out var adminId);
            var result = await _wirelessPropertiesSettingsService.DeleteExecuteNowBulkAsync(request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return MapBulkError(result.ErrorCode, result.Message, request.Options.CorrelationId);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(DeleteExecuteNowBulk), ex, cancellationToken);
        }
    }

    [HttpPost("delete/execute-now/group/{groupId:guid}")]
    public async Task<ActionResult<WindowsWirelessPropertiesBulkResponse>> DeleteExecuteNowGroup(
        Guid groupId,
        [FromBody] WindowsWirelessPropertiesDeleteExecuteNowGroupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows Wireless Properties endpoint is disabled.", request.Options.CorrelationId));
            }

            request.GroupId = groupId;

            var validation = await _deleteExecuteNowGroupValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError(
                    "ValidationFailed",
                    string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                    request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;
            TryGetAdminId(out var adminId);
            var result = await _wirelessPropertiesSettingsService.DeleteExecuteNowGroupAsync(groupId, request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return MapBulkError(result.ErrorCode, result.Message, request.Options.CorrelationId);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(DeleteExecuteNowGroup), ex, cancellationToken);
        }
    }

    [HttpPost("delete/queue")]
    public async Task<ActionResult<WindowsWirelessPropertiesDeleteQueueResponse>> DeleteQueue(
        [FromBody] WindowsWirelessPropertiesDeleteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows Wireless Properties endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _deleteValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError(
                    "ValidationFailed",
                    string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                    request.Options.CorrelationId));
            }

            if (!string.Equals(request.Execution.ScheduleType?.Trim(), "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(BuildError(
                    "ValidationFailed",
                    "scheduleType must be Queue for delete queue.",
                    request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;
            TryGetAdminId(out var adminId);
            var result = await _wirelessPropertiesSettingsService.DeleteQueueAsync(request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return MapWriteError(result.ErrorCode, result.Message, request.Options.CorrelationId);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(DeleteQueue), ex, cancellationToken);
        }
    }

    private async Task<ActionResult<TResponse>> MapWriteResultAsync<TResponse, TResult>(
        Guid? correlationId,
        bool enabled,
        FluentValidation.Results.ValidationResult validation,
        Func<Task<TResult>> invoke,
        Func<TResult, TResponse?> getResponse,
        Func<TResult, string?> getErrorCode,
        Func<TResult, string?> getMessage)
        where TResult : class
    {
        if (!enabled)
        {
            return NotFound(BuildError("FeatureDisabled", "Windows Wireless Properties endpoint is disabled.", correlationId));
        }

        if (!validation.IsValid)
        {
            return BadRequest(BuildError(
                "ValidationFailed",
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                correlationId));
        }

        var result = await invoke();
        var response = getResponse(result);
        if (response is not null)
        {
            return Ok(response);
        }

        return MapWriteError(getErrorCode(result), getMessage(result), correlationId);
    }

    private ActionResult MapReadError(string? errorCode, string? message, Guid correlationId) =>
        errorCode switch
        {
            "DeviceNotFound" => NotFound(BuildError("DeviceNotFound", message ?? "Target device was not found.", correlationId)),
            "ProfileNotFound" => NotFound(BuildError("ProfileNotFound", message ?? "Wireless profile was not found.", correlationId)),
            "ValidationFailed" => BadRequest(BuildError("ValidationFailed", message ?? "Invalid request.", correlationId)),
            _ => StatusCode(StatusCodes.Status502BadGateway, BuildError(
                errorCode ?? "LegacyBehaviorExecutionFailed",
                message ?? "Read operation failed.",
                correlationId))
        };

    private ActionResult MapBulkError(string? errorCode, string? message, Guid? correlationId) =>
        errorCode switch
        {
            "ValidationFailed" => BadRequest(BuildError("ValidationFailed", message ?? "Invalid request.", correlationId)),
            "GroupNotFound" => NotFound(BuildError("GroupNotFound", message ?? "Target group was not found.", correlationId)),
            "FeatureDisabled" => NotFound(BuildError("FeatureDisabled", message ?? "Windows Wireless Properties endpoint is disabled.", correlationId)),
            _ => StatusCode(StatusCodes.Status502BadGateway, BuildError(
                errorCode ?? "LegacyBehaviorExecutionFailed",
                message ?? "Bulk operation failed.",
                correlationId))
        };

    private ActionResult MapWriteError(string? errorCode, string? message, Guid? correlationId) =>
        errorCode switch
        {
            "ValidationFailed" => BadRequest(BuildError("ValidationFailed", message ?? "Invalid request.", correlationId)),
            "DeviceNotFound" => NotFound(BuildError("DeviceNotFound", message ?? "Target device was not found.", correlationId)),
            "ProfileNotFound" => NotFound(BuildError("ProfileNotFound", message ?? "Wireless profile was not found.", correlationId)),
            "ProfileAlreadyExists" => Conflict(BuildError("ProfileAlreadyExists", message ?? "Wireless profile already exists.", correlationId)),
            "ApplyBlocked" => Conflict(BuildError("ApplyBlocked", message ?? "Apply is blocked for this device.", correlationId)),
            "FeatureDisabled" => NotFound(BuildError("FeatureDisabled", message ?? "Windows Wireless Properties endpoint is disabled.", correlationId)),
            "LegacyBehaviorExecutionFailed" => StatusCode(StatusCodes.Status502BadGateway, BuildError(
                "LegacyBehaviorExecutionFailed",
                message ?? "Legacy compatibility behavior failed.",
                correlationId)),
            _ => StatusCode(StatusCodes.Status502BadGateway, BuildError(
                errorCode ?? "LegacyBehaviorExecutionFailed",
                message ?? "Write operation failed.",
                correlationId))
        };

    private async Task<ObjectResult> HandleUnexpectedExceptionAsync(
        string actionName,
        Exception ex,
        CancellationToken cancellationToken = default)
    {
        Guid? adminId = TryGetAdminId(out var id) ? id : null;
        return await this.HandleUnexpectedExceptionAsync(
            _exceptionLogWriter,
            _logger,
            actionName,
            ex,
            adminId: adminId,
            cancellationToken: cancellationToken);
    }

    private bool TryGetAdminId(out Guid adminId)
    {
        adminId = default;
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
                      User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      User.FindFirstValue("sub");

        return !string.IsNullOrWhiteSpace(subject) && Guid.TryParse(subject, out adminId);
    }

    private static WindowsWirelessPropertiesErrorResponse BuildError(string error, string message, Guid? correlationId) =>
        new()
        {
            Error = error,
            Message = message,
            CorrelationId = correlationId ?? Guid.NewGuid()
        };
}
