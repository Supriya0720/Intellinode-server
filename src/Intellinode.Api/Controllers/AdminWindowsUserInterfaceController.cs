using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentValidation;
using Intellinode.Api.Http;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Application.Validation;
using Intellinode.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Intellinode.Api.Controllers;

[ApiController]
[Route("api/v1/admin/device-config/user-interface")]
[Authorize(Roles = "Admin")]
public sealed class AdminWindowsUserInterfaceController : ControllerBase
{
    private readonly IWindowsUserInterfaceSettingsService _userInterfaceSettingsService;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<AdminWindowsUserInterfaceController> _logger;
    private readonly IValidator<WindowsUserInterfaceExecuteNowRequest> _executeNowValidator;
    private readonly IValidator<WindowsUserInterfaceQueueRequest> _queueValidator;
    private readonly IValidator<WindowsUserInterfaceHistoryQuery> _historyQueryValidator;
    private readonly WindowsUserInterfaceOptions _options;

    public AdminWindowsUserInterfaceController(
        IWindowsUserInterfaceSettingsService userInterfaceSettingsService,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<AdminWindowsUserInterfaceController> logger,
        IValidator<WindowsUserInterfaceExecuteNowRequest> executeNowValidator,
        IValidator<WindowsUserInterfaceQueueRequest> queueValidator,
        IValidator<WindowsUserInterfaceHistoryQuery> historyQueryValidator,
        IOptions<WindowsUserInterfaceOptions> options)
    {
        _userInterfaceSettingsService = userInterfaceSettingsService;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
        _executeNowValidator = executeNowValidator;
        _queueValidator = queueValidator;
        _historyQueryValidator = historyQueryValidator;
        _options = options.Value;
    }

    [HttpGet("{macAddress}")]
    public async Task<ActionResult<WindowsUserInterfaceCurrentResponse>> GetCurrent(
        string macAddress,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();
            if (!_options.Enabled)
            {
                return NotFound(BuildError("FeatureDisabled", "User interface endpoint is disabled.", correlationId));
            }

            if (string.IsNullOrWhiteSpace(macAddress))
            {
                return BadRequest(BuildError("ValidationFailed", "macAddress is required.", correlationId));
            }

            var result = await _userInterfaceSettingsService.GetCurrentAsync(macAddress.Trim(), cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return result.ErrorCode switch
            {
                "DeviceNotFound" => NotFound(BuildError(
                    "DeviceNotFound",
                    result.Message ?? "Target device was not found.",
                    correlationId)),
                "ValidationFailed" => BadRequest(BuildError(
                    "ValidationFailed",
                    result.Message ?? "Invalid request.",
                    correlationId)),
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

    [HttpGet("{macAddress}/users")]
    public async Task<ActionResult<WindowsUserInterfaceUsersResponse>> GetUsers(
        string macAddress,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();
            if (!_options.Enabled)
            {
                return NotFound(BuildError("FeatureDisabled", "User interface users endpoint is disabled.", correlationId));
            }

            if (string.IsNullOrWhiteSpace(macAddress))
            {
                return BadRequest(BuildError("ValidationFailed", "macAddress is required.", correlationId));
            }

            var result = await _userInterfaceSettingsService.GetUsersAsync(macAddress.Trim(), cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return result.ErrorCode switch
            {
                "DeviceNotFound" => NotFound(BuildError(
                    "DeviceNotFound",
                    result.Message ?? "Target device was not found.",
                    correlationId)),
                "ValidationFailed" => BadRequest(BuildError(
                    "ValidationFailed",
                    result.Message ?? "Invalid request.",
                    correlationId)),
                _ => StatusCode(StatusCodes.Status502BadGateway, BuildError(
                    result.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    result.Message ?? "Users operation failed.",
                    correlationId))
            };
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(GetUsers), ex, cancellationToken);
        }
    }

    [HttpGet("apply-history/{macAddress}")]
    public async Task<ActionResult<WindowsUserInterfaceHistoryResponse>> GetApplyHistory(
        string macAddress,
        [FromQuery] WindowsUserInterfaceHistoryQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();
            if (!_options.Enabled)
            {
                return NotFound(BuildError("FeatureDisabled", "User interface endpoint is disabled.", correlationId));
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

            var result = await _userInterfaceSettingsService.GetApplyHistoryAsync(macAddress.Trim(), query, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return result.ErrorCode switch
            {
                "DeviceNotFound" => NotFound(BuildError(
                    "DeviceNotFound",
                    result.Message ?? "Target device was not found.",
                    correlationId)),
                "ValidationFailed" => BadRequest(BuildError(
                    "ValidationFailed",
                    result.Message ?? "Invalid request.",
                    correlationId)),
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
    public async Task<ActionResult<WindowsUserInterfaceQueueResponse>> Queue(
        [FromBody] WindowsUserInterfaceQueueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "User interface endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _queueValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError(
                    "ValidationFailed",
                    string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                    request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _userInterfaceSettingsService.QueueAsync(request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return MapApplyFailure(result.ErrorCode, result.Message, request.Options.CorrelationId);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(Queue), ex, cancellationToken);
        }
    }

    [HttpPost("execute-now")]
    public async Task<ActionResult<WindowsUserInterfaceExecuteNowResponse>> ExecuteNow(
        [FromBody] WindowsUserInterfaceExecuteNowRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "User interface endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _executeNowValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError(
                    "ValidationFailed",
                    string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                    request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _userInterfaceSettingsService.ExecuteNowAsync(request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return MapApplyFailure(result.ErrorCode, result.Message, request.Options.CorrelationId);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(ExecuteNow), ex, cancellationToken);
        }
    }

    [HttpPost("template-queue")]
    public async Task<ActionResult<WindowsUserInterfaceQueueResponse>> TemplateQueue(
        [FromBody] WindowsUserInterfaceTemplateQueueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "User interface endpoint is disabled.", request.Options.CorrelationId));
            }

            var validator = HttpContext.RequestServices.GetRequiredService<IValidator<WindowsUserInterfaceTemplateQueueRequest>>();
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError(
                    "ValidationFailed",
                    string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                    request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _userInterfaceSettingsService.TemplateQueueAsync(request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return MapApplyFailure(result.ErrorCode, result.Message, request.Options.CorrelationId);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(TemplateQueue), ex, cancellationToken);
        }
    }

    [HttpPost("execute-now/bulk")]
    public async Task<ActionResult<WindowsUserInterfaceBulkResponse>> ExecuteNowBulk(
        [FromBody] WindowsUserInterfaceExecuteNowBulkRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "User interface endpoint is disabled.", request.Options.CorrelationId));
            }

            var validator = HttpContext.RequestServices.GetRequiredService<IValidator<WindowsUserInterfaceExecuteNowBulkRequest>>();
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError(
                    "ValidationFailed",
                    string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                    request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _userInterfaceSettingsService.ExecuteNowBulkAsync(request, adminId, cancellationToken);
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
    public async Task<ActionResult<WindowsUserInterfaceBulkResponse>> ExecuteNowGroup(
        Guid groupId,
        [FromBody] WindowsUserInterfaceExecuteNowGroupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "User interface endpoint is disabled.", request.Options.CorrelationId));
            }

            request.GroupId = groupId;
            var validator = HttpContext.RequestServices.GetRequiredService<IValidator<WindowsUserInterfaceExecuteNowGroupRequest>>();
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError(
                    "ValidationFailed",
                    string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                    request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _userInterfaceSettingsService.ExecuteNowGroupAsync(groupId, request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return result.ErrorCode switch
            {
                "GroupNotFound" => NotFound(BuildError(
                    "GroupNotFound",
                    result.Message ?? "Target group was not found.",
                    request.Options.CorrelationId)),
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

    private ObjectResult MapApplyFailure(string? errorCode, string? message, Guid? correlationId) =>
        (errorCode ?? string.Empty) switch
        {
            "ValidationFailed" => BadRequest(BuildError("ValidationFailed", message ?? "Invalid request.", correlationId)),
            "DeviceNotFound" => NotFound(BuildError("DeviceNotFound", message ?? "Target device was not found.", correlationId)),
            "ApplyBlocked" => Conflict(BuildError(
                "ApplyBlocked",
                WindowsUserInterfaceApplyBlockReason.FormatFusionXMessage(message),
                correlationId)),
            "LegacyBehaviorExecutionFailed" => StatusCode(StatusCodes.Status502BadGateway, BuildError("LegacyBehaviorExecutionFailed", message ?? "Legacy compatibility behavior failed.", correlationId)),
            _ => StatusCode(StatusCodes.Status502BadGateway, BuildError(errorCode ?? "LegacyBehaviorExecutionFailed", message ?? "Apply failed.", correlationId))
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

    private static WindowsUserInterfaceErrorResponse BuildError(string error, string message, Guid? correlationId) =>
        new()
        {
            Error = error,
            Message = message,
            CorrelationId = correlationId ?? Guid.NewGuid()
        };
}
