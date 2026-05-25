using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Intellinode.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly IAdminAuthService _adminAuthService;
    private readonly IAgentEnrollmentService _enrollmentService;
    private readonly IAgentTaskService _agentTaskService;
    private readonly IValidator<AdminLoginRequest> _loginValidator;
    private readonly IValidator<AdminQueueTaskRequest> _queueTaskValidator;

    public AdminController(
        IAdminAuthService adminAuthService,
        IAgentEnrollmentService enrollmentService,
        IAgentTaskService agentTaskService,
        IValidator<AdminLoginRequest> loginValidator,
        IValidator<AdminQueueTaskRequest> queueTaskValidator)
    {
        _adminAuthService = adminAuthService;
        _enrollmentService = enrollmentService;
        _agentTaskService = agentTaskService;
        _loginValidator = loginValidator;
        _queueTaskValidator = queueTaskValidator;
    }

    [HttpPost("auth/login")]
    [AllowAnonymous]
    public async Task<ActionResult<AdminLoginResponse>> Login(
        [FromBody] AdminLoginRequest request,
        CancellationToken cancellationToken)
    {
        await _loginValidator.ValidateAndThrowAsync(request, cancellationToken);
        var response = await _adminAuthService.LoginAsync(request, cancellationToken);
        return response is null ? Unauthorized() : Ok(response);
    }

    /// <summary>
    /// Builds {ServerBaseUrl}/enroll?token=... for agent onboarding.
    /// </summary>
    [HttpGet("agents/enrollment-link")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminEnrollmentLinkResponse>> CreateEnrollmentLink(
        [FromQuery] string? macAddress,
        CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out var adminId))
        {
            return Unauthorized();
        }

        var link = await _enrollmentService.CreateEnrollmentLinkAsync(adminId, macAddress, cancellationToken);
        return Ok(link);
    }

    [HttpGet("health")]
    [Authorize(Roles = "Admin")]
    public IActionResult Health() => Ok(new { status = "ok", service = "Intellinode" });

    /// <summary>
    /// Queues a device task (FusionX Task_Schedule_Details equivalent).
    /// </summary>
    [HttpPost("devices/{macAddress}/tasks")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminQueueTaskResponse>> QueueDeviceTask(
        string macAddress,
        [FromBody] AdminQueueTaskRequest request,
        CancellationToken cancellationToken)
    {
        await _queueTaskValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await _agentTaskService.QueueTaskForDeviceAsync(
            TenantDefaults.DefaultTenantId,
            macAddress,
            request,
            cancellationToken);

        if (result is null)
        {
            return NotFound(new AgentErrorResponse
            {
                Error = "DeviceNotFound",
                Message = $"No device found with MAC address '{macAddress.Trim()}'."
            });
        }

        return CreatedAtAction(nameof(QueueDeviceTask), new { macAddress }, result);
    }

    private bool TryGetAdminId(out Guid adminId)
    {
        adminId = default;
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
                      User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      User.FindFirstValue("sub");

        return !string.IsNullOrWhiteSpace(subject) && Guid.TryParse(subject, out adminId);
    }
}
