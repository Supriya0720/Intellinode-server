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
[Route("api/v1/admin/device-config/application-command")]
[Authorize(Roles = "Admin")]
public sealed class AdminWindowsApplicationCommandController : ControllerBase
{
    private readonly IWindowsApplicationCommandSettingsService _applicationCommandSettingsService;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<AdminWindowsApplicationCommandController> _logger;
    private readonly IValidator<WindowsApplicationCommandExecuteNowRequest> _executeNowValidator;
    private readonly IValidator<WindowsApplicationCommandQueueRequest> _queueValidator;
    private readonly IValidator<WindowsApplicationCommandTemplateQueueRequest> _templateQueueValidator;
    private readonly IValidator<WindowsApplicationCommandExecuteNowBulkRequest> _executeNowBulkValidator;
    private readonly IValidator<WindowsApplicationCommandExecuteNowGroupRequest> _executeNowGroupValidator;
    private readonly IValidator<WindowsApplicationCommandHistoryQuery> _historyQueryValidator;
    private readonly WindowsApplicationCommandOptions _options;

    public AdminWindowsApplicationCommandController(
        IWindowsApplicationCommandSettingsService applicationCommandSettingsService,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<AdminWindowsApplicationCommandController> logger,
        IValidator<WindowsApplicationCommandExecuteNowRequest> executeNowValidator,
        IValidator<WindowsApplicationCommandQueueRequest> queueValidator,
        IValidator<WindowsApplicationCommandTemplateQueueRequest> templateQueueValidator,
        IValidator<WindowsApplicationCommandExecuteNowBulkRequest> executeNowBulkValidator,
        IValidator<WindowsApplicationCommandExecuteNowGroupRequest> executeNowGroupValidator,
        IValidator<WindowsApplicationCommandHistoryQuery> historyQueryValidator,
        IOptions<WindowsApplicationCommandOptions> options)
    {
        _applicationCommandSettingsService = applicationCommandSettingsService;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
        _executeNowValidator = executeNowValidator;
        _queueValidator = queueValidator;
        _templateQueueValidator = templateQueueValidator;
        _executeNowBulkValidator = executeNowBulkValidator;
        _executeNowGroupValidator = executeNowGroupValidator;
        _historyQueryValidator = historyQueryValidator;
        _options = options.Value;
    }

    [HttpGet("{macAddress}")]
    public async Task<ActionResult<WindowsApplicationCommandCurrentResponse>> GetCurrent(
        string macAddress,
        [FromQuery] string? mode,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();
            if (!_options.Enabled)
            {
                return NotFound(BuildError("FeatureDisabled", "Application command endpoint is disabled.", correlationId));
            }

            if (string.IsNullOrWhiteSpace(macAddress))
            {
                return BadRequest(BuildError("ValidationFailed", "macAddress is required.", correlationId));
            }

            var result = await _applicationCommandSettingsService.GetCurrentAsync(
                macAddress.Trim(),
                mode,
                cancellationToken);
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

    [HttpGet("apply-history/{macAddress}")]
    public async Task<ActionResult<WindowsApplicationCommandHistoryResponse>> GetApplyHistory(
        string macAddress,
        [FromQuery] WindowsApplicationCommandHistoryQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();
            if (!_options.Enabled)
            {
                return NotFound(BuildError("FeatureDisabled", "Application command endpoint is disabled.", correlationId));
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

            var result = await _applicationCommandSettingsService.GetApplyHistoryAsync(
                macAddress.Trim(),
                query,
                cancellationToken);
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
                    result.Message ?? "History read failed.",
                    correlationId))
            };
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(GetApplyHistory), ex, cancellationToken);
        }
    }

    [HttpPost("queue")]
    public async Task<ActionResult<WindowsApplicationCommandQueueResponse>> Queue(
        [FromBody] WindowsApplicationCommandQueueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Application command endpoint is disabled.", request.Options.CorrelationId));
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
            var result = await _applicationCommandSettingsService.QueueAsync(request, adminId, cancellationToken);
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
    public async Task<ActionResult<WindowsApplicationCommandExecuteNowResponse>> ExecuteNow(
        [FromBody] WindowsApplicationCommandExecuteNowRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Application command endpoint is disabled.", request.Options.CorrelationId));
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
            var result = await _applicationCommandSettingsService.ExecuteNowAsync(request, adminId, cancellationToken);
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
    public async Task<ActionResult<WindowsApplicationCommandQueueResponse>> TemplateQueue(
        [FromBody] WindowsApplicationCommandTemplateQueueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Application command endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _templateQueueValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError(
                    "ValidationFailed",
                    string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                    request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _applicationCommandSettingsService.TemplateQueueAsync(request, adminId, cancellationToken);
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
    public async Task<ActionResult<WindowsApplicationCommandBulkResponse>> ExecuteNowBulk(
        [FromBody] WindowsApplicationCommandExecuteNowBulkRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Application command endpoint is disabled.", request.Options.CorrelationId));
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
            var result = await _applicationCommandSettingsService.ExecuteNowBulkAsync(request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return result.ErrorCode switch
            {
                "ValidationFailed" => BadRequest(BuildError("ValidationFailed", result.Message ?? "Invalid request.", request.Options.CorrelationId)),
                "GroupNotFound" => NotFound(BuildError("GroupNotFound", result.Message ?? "Group not found.", request.Options.CorrelationId)),
                _ => StatusCode(StatusCodes.Status502BadGateway, BuildError(
                    result.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    result.Message ?? "Bulk execute-now failed.",
                    request.Options.CorrelationId))
            };
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(ExecuteNowBulk), ex, cancellationToken);
        }
    }

    [HttpPost("execute-now/group/{groupId:guid}")]
    public async Task<ActionResult<WindowsApplicationCommandBulkResponse>> ExecuteNowGroup(
        Guid groupId,
        [FromBody] WindowsApplicationCommandExecuteNowGroupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Application command endpoint is disabled.", request.Options.CorrelationId));
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
            var result = await _applicationCommandSettingsService.ExecuteNowGroupAsync(groupId, request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return result.ErrorCode switch
            {
                "ValidationFailed" => BadRequest(BuildError("ValidationFailed", result.Message ?? "Invalid request.", request.Options.CorrelationId)),
                "GroupNotFound" => NotFound(BuildError("GroupNotFound", result.Message ?? "Group not found.", request.Options.CorrelationId)),
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
                WindowsApplicationCommandApplyBlockReason.FormatFusionXMessage(message),
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

    private static WindowsApplicationCommandErrorResponse BuildError(string error, string message, Guid? correlationId) =>
        new()
        {
            Error = error,
            Message = message,
            CorrelationId = correlationId ?? Guid.NewGuid()
        };
}
