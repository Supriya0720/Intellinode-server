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
[Route("api/v1/admin/device-config/wallpaper")]
[Authorize(Roles = "Admin")]
public sealed class AdminWindowsWallpaperController : ControllerBase
{
    private readonly IWindowsWallpaperSettingsService _wallpaperSettingsService;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<AdminWindowsWallpaperController> _logger;
    private readonly IValidator<WindowsWallpaperExecuteNowRequest> _executeNowValidator;
    private readonly IValidator<WindowsWallpaperQueueRequest> _queueValidator;
    private readonly IValidator<WindowsWallpaperTemplateQueueRequest> _templateQueueValidator;
    private readonly IValidator<WindowsWallpaperExecuteNowBulkRequest> _executeNowBulkValidator;
    private readonly IValidator<WindowsWallpaperExecuteNowGroupRequest> _executeNowGroupValidator;
    private readonly IValidator<WindowsWallpaperHistoryQuery> _historyQueryValidator;
    private readonly WindowsWallpaperOptions _options;

    public AdminWindowsWallpaperController(
        IWindowsWallpaperSettingsService wallpaperSettingsService,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<AdminWindowsWallpaperController> logger,
        IValidator<WindowsWallpaperExecuteNowRequest> executeNowValidator,
        IValidator<WindowsWallpaperQueueRequest> queueValidator,
        IValidator<WindowsWallpaperTemplateQueueRequest> templateQueueValidator,
        IValidator<WindowsWallpaperExecuteNowBulkRequest> executeNowBulkValidator,
        IValidator<WindowsWallpaperExecuteNowGroupRequest> executeNowGroupValidator,
        IValidator<WindowsWallpaperHistoryQuery> historyQueryValidator,
        IOptions<WindowsWallpaperOptions> options)
    {
        _wallpaperSettingsService = wallpaperSettingsService;
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
    public async Task<ActionResult<WindowsWallpaperCurrentResponse>> GetCurrent(
        string macAddress,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();
            if (!_options.Enabled)
            {
                return NotFound(BuildError("FeatureDisabled", "Wallpaper endpoint is disabled.", correlationId));
            }

            if (string.IsNullOrWhiteSpace(macAddress))
            {
                return BadRequest(BuildError("ValidationFailed", "macAddress is required.", correlationId));
            }

            var result = await _wallpaperSettingsService.GetCurrentAsync(macAddress.Trim(), cancellationToken);
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
    public async Task<ActionResult<WindowsWallpaperHistoryResponse>> GetApplyHistory(
        string macAddress,
        [FromQuery] WindowsWallpaperHistoryQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();
            if (!_options.Enabled)
            {
                return NotFound(BuildError("FeatureDisabled", "Wallpaper endpoint is disabled.", correlationId));
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

            var result = await _wallpaperSettingsService.GetApplyHistoryAsync(macAddress.Trim(), query, cancellationToken);
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
    public async Task<ActionResult<WindowsWallpaperQueueResponse>> Queue(
        [FromBody] WindowsWallpaperQueueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Wallpaper endpoint is disabled.", request.Options.CorrelationId));
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
            var result = await _wallpaperSettingsService.QueueAsync(request, adminId, cancellationToken);
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
    public async Task<ActionResult<WindowsWallpaperExecuteNowResponse>> ExecuteNow(
        [FromBody] WindowsWallpaperExecuteNowRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Wallpaper endpoint is disabled.", request.Options.CorrelationId));
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
            var result = await _wallpaperSettingsService.ExecuteNowAsync(request, adminId, cancellationToken);
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
    public async Task<ActionResult<WindowsWallpaperQueueResponse>> TemplateQueue(
        [FromBody] WindowsWallpaperTemplateQueueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Wallpaper endpoint is disabled.", request.Options.CorrelationId));
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
            var result = await _wallpaperSettingsService.TemplateQueueAsync(request, adminId, cancellationToken);
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
    public async Task<ActionResult<WindowsWallpaperBulkResponse>> ExecuteNowBulk(
        [FromBody] WindowsWallpaperExecuteNowBulkRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Wallpaper endpoint is disabled.", request.Options.CorrelationId));
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
            var result = await _wallpaperSettingsService.ExecuteNowBulkAsync(request, adminId, cancellationToken);
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
    public async Task<ActionResult<WindowsWallpaperBulkResponse>> ExecuteNowGroup(
        Guid groupId,
        [FromBody] WindowsWallpaperExecuteNowGroupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Wallpaper endpoint is disabled.", request.Options.CorrelationId));
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
            var result = await _wallpaperSettingsService.ExecuteNowGroupAsync(groupId, request, adminId, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return StatusCode(StatusCodes.Status502BadGateway, BuildError(
                result.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                result.Message ?? "Group execute-now failed.",
                request.Options.CorrelationId));
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
            "ApplyBlocked" => Conflict(BuildError("ApplyBlocked", message ?? "Apply is blocked for this device.", correlationId)),
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

    private static WindowsWallpaperErrorResponse BuildError(string error, string message, Guid? correlationId) =>
        new()
        {
            Error = error,
            Message = message,
            CorrelationId = correlationId ?? Guid.NewGuid()
        };
}
