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
[Route("api/v1/admin/systemsetting/remote-settings")]
[Authorize(Roles = "Admin")]
public sealed class AdminSystemSettingController : ControllerBase
{
    private readonly ISystemSettingService _systemSettingService;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<AdminSystemSettingController> _logger;
    private readonly IValidator<SystemSettingExecuteNowRequest> _executeNowValidator;
    private readonly IValidator<SystemSettingExecuteNowBulkRequest> _bulkValidator;
    private readonly IValidator<SystemSettingQueueRequest> _queueValidator;
    private readonly IValidator<SystemSettingTemplateQueueRequest> _templateQueueValidator;
    private readonly IValidator<SystemSettingHistoryQuery> _historyQueryValidator;
    private readonly SystemSettingOptions _options;

    public AdminSystemSettingController(
        ISystemSettingService systemSettingService,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<AdminSystemSettingController> logger,
        IValidator<SystemSettingExecuteNowRequest> executeNowValidator,
        IValidator<SystemSettingExecuteNowBulkRequest> bulkValidator,
        IValidator<SystemSettingQueueRequest> queueValidator,
        IValidator<SystemSettingTemplateQueueRequest> templateQueueValidator,
        IValidator<SystemSettingHistoryQuery> historyQueryValidator,
        IOptions<SystemSettingOptions> options)
    {
        _systemSettingService = systemSettingService;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
        _executeNowValidator = executeNowValidator;
        _bulkValidator = bulkValidator;
        _queueValidator = queueValidator;
        _templateQueueValidator = templateQueueValidator;
        _historyQueryValidator = historyQueryValidator;
        _options = options.Value;
    }

    [HttpGet("{macAddress}")]
    public async Task<ActionResult<SystemSettingCurrentResponse>> GetCurrent(
        string macAddress,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();
            if (!_options.Enabled)
            {
                return NotFound(BuildError("FeatureDisabled", "SystemSetting compatibility endpoint is disabled.", correlationId));
            }

            if (string.IsNullOrWhiteSpace(macAddress))
            {
                return BadRequest(BuildError("ValidationFailed", "macAddress is required.", correlationId));
            }

            var result = await _systemSettingService.GetCurrentAsync(macAddress.Trim(), cancellationToken);
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
    public async Task<ActionResult<SystemSettingHistoryResponse>> GetApplyHistory(
        string macAddress,
        [FromQuery] SystemSettingHistoryQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();
            if (!_options.Enabled)
            {
                return NotFound(BuildError("FeatureDisabled", "SystemSetting compatibility endpoint is disabled.", correlationId));
            }

            if (string.IsNullOrWhiteSpace(macAddress))
            {
                return BadRequest(BuildError("ValidationFailed", "macAddress is required.", correlationId));
            }

            var validation = await _historyQueryValidator.ValidateAsync(query, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError("ValidationFailed", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), correlationId));
            }

            var result = await _systemSettingService.GetApplyHistoryAsync(macAddress.Trim(), query, cancellationToken);
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

    [HttpPost("execute-now")]
    public async Task<ActionResult<SystemSettingExecuteNowResponse>> ExecuteNow(
        [FromBody] SystemSettingExecuteNowRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "SystemSetting compatibility endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _executeNowValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError("ValidationFailed", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), request.Options.CorrelationId));
            }
            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _systemSettingService.ExecuteNowAsync(request, adminId, cancellationToken);
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
    public async Task<ActionResult<SystemSettingBulkResponse>> ExecuteNowBulk(
        [FromBody] SystemSettingExecuteNowBulkRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "SystemSetting compatibility endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _bulkValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError("ValidationFailed", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), request.Options.CorrelationId));
            }
            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _systemSettingService.ExecuteNowBulkAsync(request, adminId, cancellationToken);
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

    [HttpPost("queue")]
    public async Task<ActionResult<SystemSettingQueueResponse>> Queue(
        [FromBody] SystemSettingQueueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "SystemSetting compatibility endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _queueValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError("ValidationFailed", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), request.Options.CorrelationId));
            }
            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _systemSettingService.QueueAsync(request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return result.ErrorCode switch
            {
                "DeviceNotFound" => NotFound(BuildError("DeviceNotFound", result.Message ?? "Target device was not found.", request.Options.CorrelationId)),
                "ApplyBlocked" => Conflict(BuildError("ApplyBlocked", result.Message ?? "Queue apply is blocked for this device.", request.Options.CorrelationId)),
                _ => StatusCode(StatusCodes.Status502BadGateway, BuildError(
                    result.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    result.Message ?? "Queue operation failed.",
                    request.Options.CorrelationId))
            };
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(Queue), ex, cancellationToken);
        }
    }

    [HttpPost("template-queue")]
    public async Task<ActionResult<SystemSettingQueueResponse>> TemplateQueue(
        [FromBody] SystemSettingTemplateQueueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "SystemSetting compatibility endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _templateQueueValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError("ValidationFailed", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), request.Options.CorrelationId));
            }
            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _systemSettingService.TemplateQueueAsync(request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return result.ErrorCode switch
            {
                "DeviceNotFound" => NotFound(BuildError("DeviceNotFound", result.Message ?? "Target device was not found.", request.Options.CorrelationId)),
                "ApplyBlocked" => Conflict(BuildError("ApplyBlocked", result.Message ?? "Template queue apply is blocked for this device.", request.Options.CorrelationId)),
                _ => StatusCode(StatusCodes.Status502BadGateway, BuildError(
                    result.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    result.Message ?? "Template queue operation failed.",
                    request.Options.CorrelationId))
            };
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(TemplateQueue), ex, cancellationToken);
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

    private static SystemSettingErrorResponse BuildError(string error, string message, Guid? correlationId) =>
        new()
        {
            Error = error,
            Message = message,
            CorrelationId = correlationId ?? Guid.NewGuid()
        };
}
