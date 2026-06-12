using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Intellinode.Api.Http;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Intellinode.Api.Controllers;

[ApiController]
[Route("api/v1/admin/device-config/power-management/reference")]
[Authorize(Roles = "Admin")]
public sealed class AdminPowerManagementReferenceController : ControllerBase
{
    private readonly IPowerManagementReferenceService _referenceService;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<AdminPowerManagementReferenceController> _logger;
    private readonly PowerManagementReferenceOptions _options;

    public AdminPowerManagementReferenceController(
        IPowerManagementReferenceService referenceService,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<AdminPowerManagementReferenceController> logger,
        IOptions<PowerManagementReferenceOptions> options)
    {
        _referenceService = referenceService;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
        _options = options.Value;
    }

    [HttpGet("power-plans")]
    public async Task<ActionResult<TimeAndLanguageReferenceListResponse<WindowsPowerPlanMasterDto>>> GetPowerPlans(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return await GetReferenceListAsync(
            nameof(GetPowerPlans),
            ct => _referenceService.GetPowerPlansAsync(includeInactive, ct),
            cancellationToken);
    }

    [HttpGet("timeouts")]
    public async Task<ActionResult<TimeAndLanguageReferenceListResponse<WindowsPowerTimeoutMasterDto>>> GetTimeouts(
        [FromQuery] WindowsPowerTimeoutCategory? category = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return await GetReferenceListAsync(
            nameof(GetTimeouts),
            ct => _referenceService.GetTimeoutsAsync(category, includeInactive, ct),
            cancellationToken);
    }

    [HttpGet("advanced-options")]
    public async Task<ActionResult<TimeAndLanguageReferenceListResponse<WindowsPowerAdvancedOptionGroupCatalogDto>>> GetAdvancedOptions(
        [FromQuery] string? planName = null,
        [FromQuery] string? optionName = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return await GetReferenceListAsync(
            nameof(GetAdvancedOptions),
            ct => _referenceService.GetAdvancedOptionsAsync(planName, optionName, includeInactive, ct),
            cancellationToken);
    }

    private async Task<ActionResult<TimeAndLanguageReferenceListResponse<T>>> GetReferenceListAsync<T>(
        string actionName,
        Func<CancellationToken, Task<PowerManagementReferenceResult<T>>> getResult,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();
            if (!_options.Enabled)
            {
                return NotFound(BuildError(
                    "FeatureDisabled",
                    "Power management reference endpoints are disabled.",
                    correlationId));
            }

            var result = await getResult(cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return StatusCode(
                StatusCodes.Status502BadGateway,
                BuildError(
                    result.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    result.Message ?? "Read operation failed.",
                    correlationId));
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(actionName, ex, cancellationToken);
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

    private static PowerManagementReferenceErrorResponse BuildError(string error, string message, Guid? correlationId) =>
        new()
        {
            Error = error,
            Message = message,
            CorrelationId = correlationId ?? Guid.NewGuid()
        };
}
