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
[Route("api/v1/admin/device-config/windows-regional-format")]
[Authorize(Roles = "Admin")]
public sealed class AdminWindowsRegionalFormatController : ControllerBase
{
    private readonly IWindowsRegionalFormatSettingsService _regionalFormatSettingsService;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<AdminWindowsRegionalFormatController> _logger;
    private readonly IValidator<WindowsRegionalFormatExecuteNowRequest> _executeNowValidator;
    private readonly IValidator<WindowsRegionalFormatExecuteNowBulkRequest> _executeNowBulkValidator;
    private readonly IValidator<WindowsRegionalFormatExecuteNowGroupRequest> _executeNowGroupValidator;
    private readonly IValidator<WindowsRegionalFormatQueueRequest> _queueValidator;
    private readonly IValidator<WindowsRegionalFormatHistoryQuery> _historyQueryValidator;
    private readonly WindowsRegionalFormatOptions _options;

    public AdminWindowsRegionalFormatController(
        IWindowsRegionalFormatSettingsService regionalFormatSettingsService,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<AdminWindowsRegionalFormatController> logger,
        IValidator<WindowsRegionalFormatExecuteNowRequest> executeNowValidator,
        IValidator<WindowsRegionalFormatExecuteNowBulkRequest> executeNowBulkValidator,
        IValidator<WindowsRegionalFormatExecuteNowGroupRequest> executeNowGroupValidator,
        IValidator<WindowsRegionalFormatQueueRequest> queueValidator,
        IValidator<WindowsRegionalFormatHistoryQuery> historyQueryValidator,
        IOptions<WindowsRegionalFormatOptions> options)
    {
        _regionalFormatSettingsService = regionalFormatSettingsService;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
        _executeNowValidator = executeNowValidator;
        _executeNowBulkValidator = executeNowBulkValidator;
        _executeNowGroupValidator = executeNowGroupValidator;
        _queueValidator = queueValidator;
        _historyQueryValidator = historyQueryValidator;
        _options = options.Value;
    }

    [HttpGet("{macAddress}")]
    public async Task<ActionResult<WindowsRegionalFormatCurrentResponse>> GetCurrent(
        string macAddress,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();
            if (!_options.Enabled)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows regional format endpoint is disabled.", correlationId));
            }

            if (string.IsNullOrWhiteSpace(macAddress))
            {
                return BadRequest(BuildError("ValidationFailed", "macAddress is required.", correlationId));
            }

            var result = await _regionalFormatSettingsService.GetCurrentAsync(macAddress.Trim(), cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return result.ErrorCode switch
            {
                "DeviceNotFound" => NotFound(BuildError("DeviceNotFound", result.Message ?? "Target device was not found.", correlationId)),
                "ValidationFailed" => BadRequest(BuildError("ValidationFailed", result.Message ?? "Invalid request.", correlationId)),
                _ => StatusCode(StatusCodes.Status502BadGateway, BuildError(
                    result.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    result.Message ?? "Read operation failed.",
                    correlationId))
            };
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(GetCurrent), ex, cancellationToken);
        }
    }

    [HttpGet("apply-history/{macAddress}")]
    public async Task<ActionResult<WindowsRegionalFormatHistoryResponse>> GetApplyHistory(
        string macAddress,
        [FromQuery] WindowsRegionalFormatHistoryQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();
            if (!_options.Enabled)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows regional format endpoint is disabled.", correlationId));
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

            var result = await _regionalFormatSettingsService.GetApplyHistoryAsync(macAddress.Trim(), query, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return result.ErrorCode switch
            {
                "DeviceNotFound" => NotFound(BuildError("DeviceNotFound", result.Message ?? "Target device was not found.", correlationId)),
                "ValidationFailed" => BadRequest(BuildError("ValidationFailed", result.Message ?? "Invalid request.", correlationId)),
                _ => StatusCode(StatusCodes.Status502BadGateway, BuildError(
                    result.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    result.Message ?? "History operation failed.",
                    correlationId))
            };
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(GetApplyHistory), ex, cancellationToken);
        }
    }

    [HttpPost("queue")]
    public async Task<ActionResult<WindowsRegionalFormatQueueResponse>> Queue(
        [FromBody] WindowsRegionalFormatQueueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows regional format endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _queueValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError("ValidationFailed", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _regionalFormatSettingsService.QueueAsync(request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return result.ErrorCode switch
            {
                "ValidationFailed" => BadRequest(BuildError("ValidationFailed", result.Message ?? "Invalid queue payload.", request.Options.CorrelationId)),
                "DeviceNotFound" => NotFound(BuildError("DeviceNotFound", result.Message ?? "Target device was not found.", request.Options.CorrelationId)),
                "ApplyBlocked" => Conflict(BuildError("ApplyBlocked", result.Message ?? "Queue apply is blocked for this device.", request.Options.CorrelationId)),
                "LegacyBehaviorExecutionFailed" => StatusCode(StatusCodes.Status502BadGateway, BuildError("LegacyBehaviorExecutionFailed", result.Message ?? "Legacy compatibility behavior failed.", request.Options.CorrelationId)),
                _ => StatusCode(StatusCodes.Status502BadGateway, BuildError(result.ErrorCode ?? "LegacyBehaviorExecutionFailed", result.Message ?? "Queue apply failed.", request.Options.CorrelationId))
            };
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(Queue), ex, cancellationToken);
        }
    }

    [HttpPost("execute-now")]
    public async Task<ActionResult<WindowsRegionalFormatExecuteNowResponse>> ExecuteNow(
        [FromBody] WindowsRegionalFormatExecuteNowRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows regional format endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _executeNowValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError("ValidationFailed", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _regionalFormatSettingsService.ExecuteNowAsync(request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return result.ErrorCode switch
            {
                "ValidationFailed" => BadRequest(BuildError("ValidationFailed", result.Message ?? "Invalid execute-now payload.", request.Options.CorrelationId)),
                "DeviceNotFound" => NotFound(BuildError("DeviceNotFound", result.Message ?? "Target device was not found.", request.Options.CorrelationId)),
                "ApplyBlocked" => Conflict(BuildError("ApplyBlocked", result.Message ?? "Execute-now apply is blocked for this device.", request.Options.CorrelationId)),
                "LegacyBehaviorExecutionFailed" => StatusCode(StatusCodes.Status502BadGateway, BuildError("LegacyBehaviorExecutionFailed", result.Message ?? "Legacy compatibility behavior failed.", request.Options.CorrelationId)),
                _ => StatusCode(StatusCodes.Status502BadGateway, BuildError(result.ErrorCode ?? "LegacyBehaviorExecutionFailed", result.Message ?? "Execute-now apply failed.", request.Options.CorrelationId))
            };
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(ExecuteNow), ex, cancellationToken);
        }
    }

    [HttpPost("execute-now/bulk")]
    public async Task<ActionResult<WindowsRegionalFormatBulkResponse>> ExecuteNowBulk(
        [FromBody] WindowsRegionalFormatExecuteNowBulkRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows regional format endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _executeNowBulkValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError("ValidationFailed", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _regionalFormatSettingsService.ExecuteNowBulkAsync(request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return StatusCode(StatusCodes.Status502BadGateway, BuildError(
                result.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                result.Message ?? "Bulk execute-now failed.",
                request.Options.CorrelationId));
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(ExecuteNowBulk), ex, cancellationToken);
        }
    }

    [HttpPost("execute-now/group/{groupId:guid}")]
    public async Task<ActionResult<WindowsRegionalFormatBulkResponse>> ExecuteNowGroup(
        Guid groupId,
        [FromBody] WindowsRegionalFormatExecuteNowGroupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows regional format endpoint is disabled.", request.Options.CorrelationId));
            }

            request.GroupId = groupId;

            var validation = await _executeNowGroupValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError("ValidationFailed", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _regionalFormatSettingsService.ExecuteNowGroupAsync(groupId, request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return result.ErrorCode switch
            {
                "GroupNotFound" => NotFound(BuildError("GroupNotFound", result.Message ?? "Target group was not found.", request.Options.CorrelationId)),
                _ => StatusCode(StatusCodes.Status502BadGateway, BuildError(
                    result.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    result.Message ?? "Group execute-now failed.",
                    request.Options.CorrelationId))
            };
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(ExecuteNowGroup), ex, cancellationToken);
        }
    }

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

    private static WindowsRegionalFormatErrorResponse BuildError(string error, string message, Guid? correlationId) =>
        new()
        {
            Error = error,
            Message = message,
            CorrelationId = correlationId ?? Guid.NewGuid()
        };
}
