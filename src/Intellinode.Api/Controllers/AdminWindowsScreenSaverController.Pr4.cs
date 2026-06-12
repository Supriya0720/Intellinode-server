using FluentValidation;
using Intellinode.Application.Contracts.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Intellinode.Api.Controllers;

public sealed partial class AdminWindowsScreenSaverController
{
    [HttpPost("template-queue")]
    public async Task<ActionResult<WindowsScreenSaverQueueResponse>> TemplateQueue(
        [FromBody] WindowsScreenSaverTemplateQueueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Screen saver endpoint is disabled.", request.Options.CorrelationId));
            }

            var validator = HttpContext.RequestServices.GetRequiredService<IValidator<WindowsScreenSaverTemplateQueueRequest>>();
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
            var result = await _screenSaverSettingsService.TemplateQueueAsync(request, adminId, cancellationToken);
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
    public async Task<ActionResult<WindowsScreenSaverBulkResponse>> ExecuteNowBulk(
        [FromBody] WindowsScreenSaverExecuteNowBulkRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Screen saver endpoint is disabled.", request.Options.CorrelationId));
            }

            var validator = HttpContext.RequestServices.GetRequiredService<IValidator<WindowsScreenSaverExecuteNowBulkRequest>>();
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
            var result = await _screenSaverSettingsService.ExecuteNowBulkAsync(request, adminId, cancellationToken);
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
    public async Task<ActionResult<WindowsScreenSaverBulkResponse>> ExecuteNowGroup(
        Guid groupId,
        [FromBody] WindowsScreenSaverExecuteNowGroupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return NotFound(BuildError("FeatureDisabled", "Screen saver endpoint is disabled.", request.Options.CorrelationId));
            }

            request.GroupId = groupId;
            var validator = HttpContext.RequestServices.GetRequiredService<IValidator<WindowsScreenSaverExecuteNowGroupRequest>>();
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
            var result = await _screenSaverSettingsService.ExecuteNowGroupAsync(groupId, request, adminId, cancellationToken);
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
}
