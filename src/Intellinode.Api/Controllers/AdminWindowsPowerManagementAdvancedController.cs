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
[Route("api/v1/admin/device-config/windows-power-management/advanced")]
[Authorize(Roles = "Admin")]
public sealed class AdminWindowsPowerManagementAdvancedController : ControllerBase
{
    private readonly IWindowsPowerManagementSettingsService _powerManagementSettingsService;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<AdminWindowsPowerManagementAdvancedController> _logger;
    private readonly IValidator<WindowsPowerManagementAdvancedExecuteNowRequest> _executeNowValidator;
    private readonly IValidator<WindowsPowerManagementAdvancedExecuteNowBulkRequest> _executeNowBulkValidator;
    private readonly IValidator<WindowsPowerManagementAdvancedExecuteNowGroupRequest> _executeNowGroupValidator;
    private readonly IValidator<WindowsPowerManagementAdvancedQueueRequest> _queueValidator;
    private readonly IValidator<WindowsPowerManagementAdvancedTemplateQueueRequest> _templateQueueValidator;
    private readonly WindowsPowerManagementOptions _options;

    public AdminWindowsPowerManagementAdvancedController(
        IWindowsPowerManagementSettingsService powerManagementSettingsService,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<AdminWindowsPowerManagementAdvancedController> logger,
        IValidator<WindowsPowerManagementAdvancedExecuteNowRequest> executeNowValidator,
        IValidator<WindowsPowerManagementAdvancedExecuteNowBulkRequest> executeNowBulkValidator,
        IValidator<WindowsPowerManagementAdvancedExecuteNowGroupRequest> executeNowGroupValidator,
        IValidator<WindowsPowerManagementAdvancedQueueRequest> queueValidator,
        IValidator<WindowsPowerManagementAdvancedTemplateQueueRequest> templateQueueValidator,
        IOptions<WindowsPowerManagementOptions> options)
    {
        _powerManagementSettingsService = powerManagementSettingsService;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
        _executeNowValidator = executeNowValidator;
        _executeNowBulkValidator = executeNowBulkValidator;
        _executeNowGroupValidator = executeNowGroupValidator;
        _queueValidator = queueValidator;
        _templateQueueValidator = templateQueueValidator;
        _options = options.Value;
    }

    [HttpPost("queue")]
    public async Task<ActionResult<WindowsPowerManagementQueueResponse>> Queue(
        [FromBody] WindowsPowerManagementAdvancedQueueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows power management endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _queueValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError("ValidationFailed", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _powerManagementSettingsService.QueueAdvancedAsync(request, adminId, cancellationToken);
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

    [HttpPost("template-queue")]
    public async Task<ActionResult<WindowsPowerManagementQueueResponse>> TemplateQueue(
        [FromBody] WindowsPowerManagementAdvancedTemplateQueueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows power management endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _templateQueueValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError("ValidationFailed", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _powerManagementSettingsService.TemplateQueueAdvancedAsync(request, adminId, cancellationToken);
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

    [HttpPost("execute-now")]
    public async Task<ActionResult<WindowsPowerManagementExecuteNowResponse>> ExecuteNow(
        [FromBody] WindowsPowerManagementAdvancedExecuteNowRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows power management endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _executeNowValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError("ValidationFailed", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _powerManagementSettingsService.ExecuteNowAdvancedAsync(request, adminId, cancellationToken);
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

    [HttpPost("execute-now/bulk")]
    public async Task<ActionResult<WindowsPowerManagementBulkResponse>> ExecuteNowBulk(
        [FromBody] WindowsPowerManagementAdvancedExecuteNowBulkRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows power management endpoint is disabled.", request.Options.CorrelationId));
            }

            var validation = await _executeNowBulkValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError("ValidationFailed", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _powerManagementSettingsService.ExecuteNowAdvancedBulkAsync(request, adminId, cancellationToken);
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
    public async Task<ActionResult<WindowsPowerManagementBulkResponse>> ExecuteNowGroup(
        Guid groupId,
        [FromBody] WindowsPowerManagementAdvancedExecuteNowGroupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Windows power management endpoint is disabled.", request.Options.CorrelationId));
            }

            request.GroupId = groupId;

            var validation = await _executeNowGroupValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return BadRequest(BuildError("ValidationFailed", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), request.Options.CorrelationId));
            }

            request.Options.ReturnLegacySummary &= _options.LegacySummaryEnabled;

            TryGetAdminId(out var adminId);
            var result = await _powerManagementSettingsService.ExecuteNowAdvancedGroupAsync(groupId, request, adminId, cancellationToken);
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

    private ActionResult MapApplyFailure(string? errorCode, string? message, Guid? correlationId)
    {
        return errorCode switch
        {
            "ValidationFailed" => BadRequest(BuildError("ValidationFailed", message ?? "Invalid apply payload.", correlationId)),
            "DeviceNotFound" => NotFound(BuildError("DeviceNotFound", message ?? "Target device was not found.", correlationId)),
            "ApplyBlocked" => Conflict(BuildError("ApplyBlocked", message ?? "Apply is blocked for this device.", correlationId)),
            "LegacyBehaviorExecutionFailed" => StatusCode(StatusCodes.Status502BadGateway, BuildError("LegacyBehaviorExecutionFailed", message ?? "Legacy compatibility behavior failed.", correlationId)),
            _ => StatusCode(StatusCodes.Status502BadGateway, BuildError(errorCode ?? "LegacyBehaviorExecutionFailed", message ?? "Apply failed.", correlationId))
        };
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

    private static WindowsPowerManagementErrorResponse BuildError(string error, string message, Guid? correlationId) =>
        new()
        {
            Error = error,
            Message = message,
            CorrelationId = correlationId ?? Guid.NewGuid()
        };
}
