using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentValidation;
using Intellinode.Api.Http;
using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Intellinode.Api.Controllers;

[ApiController]
[Route("api/v1/agents/windows/taskbar")]
[Authorize(Roles = "Agent")]
public sealed class AgentWindowsTaskbarController : ControllerBase
{
    private readonly IWindowsTaskbarSettingsService _taskbarSettingsService;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<AgentWindowsTaskbarController> _logger;
    private readonly IValidator<AgentTaskbarLiveReportRequest> _liveReportValidator;
    private readonly WindowsTaskbarOptions _options;

    public AgentWindowsTaskbarController(
        IWindowsTaskbarSettingsService taskbarSettingsService,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<AgentWindowsTaskbarController> logger,
        IValidator<AgentTaskbarLiveReportRequest> liveReportValidator,
        IOptions<WindowsTaskbarOptions> options)
    {
        _taskbarSettingsService = taskbarSettingsService;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
        _liveReportValidator = liveReportValidator;
        _options = options.Value;
    }

    /// <summary>
    /// Agent reports live taskbar state (FusionX Input_prcGetXPTaskbarProperties write path).
    /// </summary>
    [HttpPost("live")]
    public async Task<ActionResult<AgentTaskbarLiveReportResponse>> ReportLive(
        [FromBody] AgentTaskbarLiveReportRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_options.AgentLiveReadEnabled)
            {
                return NotFound(new AgentErrorResponse
                {
                    Error = "FeatureDisabled",
                    Message = "Taskbar agent live read is disabled."
                });
            }

            await _liveReportValidator.ValidateAndThrowAsync(request, cancellationToken);

            if (!TryGetDeviceId(out var deviceId))
            {
                return Unauthorized();
            }

            var result = await _taskbarSettingsService.ReportAgentLiveAsync(deviceId, request, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Response);
            }

            return result.ErrorCode switch
            {
                "ValidationFailed" => BadRequest(new AgentErrorResponse
                {
                    Error = "ValidationFailed",
                    Message = result.Message ?? "Invalid live report payload."
                }),
                "DeviceNotFound" => NotFound(new AgentErrorResponse
                {
                    Error = "DeviceNotFound",
                    Message = result.Message ?? "Device was not found."
                }),
                _ => StatusCode(StatusCodes.Status502BadGateway, new AgentErrorResponse
                {
                    Error = result.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    Message = result.Message ?? "Live report failed."
                })
            };
        }
        catch (Exception ex)
        {
            Guid? deviceId = TryGetDeviceId(out var id) ? id : null;
            return await this.HandleUnexpectedExceptionAsync(
                _exceptionLogWriter,
                _logger,
                nameof(ReportLive),
                ex,
                deviceId,
                cancellationToken: cancellationToken);
        }
    }

    private bool TryGetDeviceId(out Guid deviceId)
    {
        deviceId = default;
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
                      User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      User.FindFirstValue("sub");

        return !string.IsNullOrWhiteSpace(subject) && Guid.TryParse(subject, out deviceId);
    }
}
