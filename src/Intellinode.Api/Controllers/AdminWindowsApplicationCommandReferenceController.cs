using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Application.Validation;
using Intellinode.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Intellinode.Api.Controllers;

[ApiController]
[Route("api/v1/admin/device-config/application-command/reference")]
[Authorize(Roles = "Admin")]
public sealed class AdminWindowsApplicationCommandReferenceController : ControllerBase
{
    private readonly WindowsApplicationCommandOptions _options;

    public AdminWindowsApplicationCommandReferenceController(
        IOptions<WindowsApplicationCommandOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// FusionX <c>Windows_ucAplicationAndCommand</c> dropdown values (message type, display time, timeout).
    /// </summary>
    [HttpGet("options")]
    public ActionResult<WindowsApplicationCommandReferenceOptionsResponse> GetOptions()
    {
        var correlationId = Guid.NewGuid();
        if (!_options.Enabled)
        {
            return NotFound(BuildError(
                "FeatureDisabled",
                "Application command reference endpoints are disabled.",
                correlationId));
        }

        return Ok(WindowsApplicationCommandReferenceCatalog.GetOptions());
    }

    [HttpGet("message-types")]
    public ActionResult<TimeAndLanguageReferenceListResponse<WindowsApplicationCommandReferenceItemDto>> GetMessageTypes()
    {
        return GetReferenceList(
            WindowsApplicationCommandReferenceCatalog.GetOptions().Data.MessageTypes,
            "Application command message types.");
    }

    [HttpGet("display-times")]
    public ActionResult<TimeAndLanguageReferenceListResponse<WindowsApplicationCommandReferenceItemDto>> GetDisplayTimes()
    {
        return GetReferenceList(
            WindowsApplicationCommandReferenceCatalog.GetOptions().Data.DisplayTimes,
            "Application command display times.");
    }

    [HttpGet("timeouts")]
    public ActionResult<TimeAndLanguageReferenceListResponse<WindowsApplicationCommandReferenceItemDto>> GetTimeouts()
    {
        return GetReferenceList(
            WindowsApplicationCommandReferenceCatalog.GetOptions().Data.Timeouts,
            "Application command timeouts.");
    }

    private ActionResult<TimeAndLanguageReferenceListResponse<WindowsApplicationCommandReferenceItemDto>> GetReferenceList(
        List<WindowsApplicationCommandReferenceItemDto> items,
        string message)
    {
        var correlationId = Guid.NewGuid();
        if (!_options.Enabled)
        {
            return NotFound(BuildError(
                "FeatureDisabled",
                "Application command reference endpoints are disabled.",
                correlationId));
        }

        return Ok(new TimeAndLanguageReferenceListResponse<WindowsApplicationCommandReferenceItemDto>
        {
            Success = true,
            Message = message,
            Data = items
        });
    }

    private static WindowsApplicationCommandReferenceErrorResponse BuildError(string error, string message, Guid? correlationId) =>
        new()
        {
            Error = error,
            Message = message,
            CorrelationId = correlationId ?? Guid.NewGuid()
        };
}
