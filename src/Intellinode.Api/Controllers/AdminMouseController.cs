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
[Route("api/v1/admin/device-config/mouse")]
[Authorize(Roles = "Admin")]
public sealed class AdminMouseController : ControllerBase
{
    private readonly IMouseSettingsService _mouseSettingsService;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<AdminMouseController> _logger;
    private readonly IValidator<MouseExecuteNowRequest> _executeNowValidator;
    private readonly IValidator<MouseQueueRequest> _queueValidator;
    private readonly IValidator<MouseHistoryQuery> _historyQueryValidator;
    private readonly MouseOptions _options;

    public AdminMouseController(
        IMouseSettingsService mouseSettingsService,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<AdminMouseController> logger,
        IValidator<MouseExecuteNowRequest> executeNowValidator,
        IValidator<MouseQueueRequest> queueValidator,
        IValidator<MouseHistoryQuery> historyQueryValidator,
        IOptions<MouseOptions> options)
    {
        _mouseSettingsService = mouseSettingsService;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
        _executeNowValidator = executeNowValidator;
        _queueValidator = queueValidator;
        _historyQueryValidator = historyQueryValidator;
        _options = options.Value;
    }

    [HttpGet("{macAddress}")]
    public async Task<ActionResult<MouseCurrentResponse>> GetCurrent(
        string macAddress,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();
            if (!_options.Enabled)
            {
                return NotFound(BuildError("FeatureDisabled", "Mouse settings endpoint is disabled.", correlationId));
            }

            if (string.IsNullOrWhiteSpace(macAddress))
            {
                return BadRequest(BuildError("ValidationFailed", "macAddress is required.", correlationId));
            }

            var result = await _mouseSettingsService.GetCurrentAsync(macAddress.Trim(), cancellationToken);
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
    public async Task<ActionResult<MouseHistoryResponse>> GetApplyHistory(
        string macAddress,
        [FromQuery] MouseHistoryQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();
            if (!_options.Enabled)
            {
                return NotFound(BuildError("FeatureDisabled", "Mouse settings endpoint is disabled.", correlationId));
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

            var result = await _mouseSettingsService.GetApplyHistoryAsync(macAddress.Trim(), query, cancellationToken);
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
    public async Task<ActionResult<MouseQueueResponse>> Queue(
        [FromBody] MouseQueueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Mouse settings endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _queueValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError("ValidationFailed", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _mouseSettingsService.QueueAsync(request, adminId, cancellationToken);
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
    public async Task<ActionResult<MouseExecuteNowResponse>> ExecuteNow(
        [FromBody] MouseExecuteNowRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Mouse settings endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _executeNowValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError("ValidationFailed", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _mouseSettingsService.ExecuteNowAsync(request, adminId, cancellationToken);
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

    private static MouseErrorResponse BuildError(string error, string message, Guid? correlationId) =>
        new()
        {
            Error = error,
            Message = message,
            CorrelationId = correlationId ?? Guid.NewGuid()
        };
}
