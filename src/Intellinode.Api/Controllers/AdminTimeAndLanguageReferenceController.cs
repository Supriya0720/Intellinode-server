using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Intellinode.Api.Http;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Infrastructure.Options;
using Intellinode.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Intellinode.Api.Controllers;

[ApiController]
[Route("api/v1/admin/device-config/time-and-language/reference")]
[Authorize(Roles = "Admin")]
public sealed class AdminTimeAndLanguageReferenceController : ControllerBase
{
    private readonly ITimeAndLanguageReferenceService _referenceService;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<AdminTimeAndLanguageReferenceController> _logger;
    private readonly TimeAndLanguageReferenceOptions _options;

    public AdminTimeAndLanguageReferenceController(
        ITimeAndLanguageReferenceService referenceService,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<AdminTimeAndLanguageReferenceController> logger,
        IOptions<TimeAndLanguageReferenceOptions> options)
    {
        _referenceService = referenceService;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
        _options = options.Value;
    }

    [HttpGet("locations")]
    public async Task<ActionResult<TimeAndLanguageReferenceListResponse<RegionLocationMasterDto>>> GetLocations(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return await GetReferenceListAsync(
            nameof(GetLocations),
            ct => _referenceService.GetLocationsAsync(includeInactive, ct),
            cancellationToken);
    }

    [HttpGet("regions")]
    public async Task<ActionResult<TimeAndLanguageReferenceListResponse<RegionLocationMasterDto>>> GetRegions(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return await GetReferenceListAsync(
            nameof(GetRegions),
            ct => _referenceService.GetRegionsAsync(includeInactive, ct),
            cancellationToken);
    }

    [HttpGet("time-zones")]
    public async Task<ActionResult<TimeAndLanguageReferenceListResponse<WindowsTimeZoneMasterDto>>> GetTimeZones(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return await GetReferenceListAsync(
            nameof(GetTimeZones),
            ct => _referenceService.GetTimeZonesAsync(includeInactive, ct),
            cancellationToken);
    }

    [HttpGet("format-presets")]
    public ActionResult<RegionalFormatPresetsResponse> GetFormatPresets()
    {
        var correlationId = Guid.NewGuid();
        if (!_options.Enabled)
        {
            return NotFound(BuildError(
                "FeatureDisabled",
                "Time and language reference endpoints are disabled.",
                correlationId));
        }

        return Ok(RegionalFormatPresets.GetPresets());
    }

    private async Task<ActionResult<TimeAndLanguageReferenceListResponse<T>>> GetReferenceListAsync<T>(
        string actionName,
        Func<CancellationToken, Task<TimeAndLanguageReferenceResult<T>>> getResult,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();
            if (!_options.Enabled)
            {
                return NotFound(BuildError(
                    "FeatureDisabled",
                    "Time and language reference endpoints are disabled.",
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

    private static TimeAndLanguageReferenceErrorResponse BuildError(string error, string message, Guid? correlationId) =>
        new()
        {
            Error = error,
            Message = message,
            CorrelationId = correlationId ?? Guid.NewGuid()
        };
}
