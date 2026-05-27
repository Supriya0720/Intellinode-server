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
    private readonly IDeviceRemoteSettingsService _remoteSettingsService;
    private readonly IDeviceAgentAdvancedSettingsService _advancedSettingsService;
    private readonly IEffectiveAgentSettingsResolver _effectiveSettingsResolver;
    private readonly IValidator<AdminLoginRequest> _loginValidator;
    private readonly IValidator<AdminQueueTaskRequest> _queueTaskValidator;
    private readonly IValidator<UpsertDeviceRemoteSettingsRequest> _remoteSettingsValidator;
    private readonly IValidator<UpsertDeviceAgentAdvancedSettingsRequest> _advancedSettingsValidator;
    private readonly IValidator<PatchDeviceSettingsInheritanceRequest> _inheritanceValidator;

    public AdminController(
        IAdminAuthService adminAuthService,
        IAgentEnrollmentService enrollmentService,
        IAgentTaskService agentTaskService,
        IDeviceRemoteSettingsService remoteSettingsService,
        IDeviceAgentAdvancedSettingsService advancedSettingsService,
        IEffectiveAgentSettingsResolver effectiveSettingsResolver,
        IValidator<AdminLoginRequest> loginValidator,
        IValidator<AdminQueueTaskRequest> queueTaskValidator,
        IValidator<UpsertDeviceRemoteSettingsRequest> remoteSettingsValidator,
        IValidator<UpsertDeviceAgentAdvancedSettingsRequest> advancedSettingsValidator,
        IValidator<PatchDeviceSettingsInheritanceRequest> inheritanceValidator)
    {
        _adminAuthService = adminAuthService;
        _enrollmentService = enrollmentService;
        _agentTaskService = agentTaskService;
        _remoteSettingsService = remoteSettingsService;
        _advancedSettingsService = advancedSettingsService;
        _effectiveSettingsResolver = effectiveSettingsResolver;
        _loginValidator = loginValidator;
        _queueTaskValidator = queueTaskValidator;
        _remoteSettingsValidator = remoteSettingsValidator;
        _advancedSettingsValidator = advancedSettingsValidator;
        _inheritanceValidator = inheritanceValidator;
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

    /// <summary>
    /// Returns desired remote agent settings for a device (FusionX Remote_Client_Settings read).
    /// When no per-device row exists, returns tenant defaults without persisting.
    /// </summary>
    [HttpGet("devices/{macAddress}/remote-settings")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DeviceRemoteSettingsDto>> GetDeviceRemoteSettings(
        string macAddress,
        CancellationToken cancellationToken)
    {
        var settings = await _remoteSettingsService.GetByMacAsync(macAddress, cancellationToken);
        if (settings is null)
        {
            return NotFound(new AgentErrorResponse
            {
                Error = "DeviceNotFound",
                Message = $"No device found with MAC address '{macAddress.Trim()}'."
            });
        }

        return Ok(settings);
    }

    /// <summary>
    /// Creates or updates desired remote agent settings for a device.
    /// Empty serverHost uses the tenant default server URL at apply time.
    /// </summary>
    [HttpPut("devices/{macAddress}/remote-settings")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DeviceRemoteSettingsDto>> UpsertDeviceRemoteSettings(
        string macAddress,
        [FromBody] UpsertDeviceRemoteSettingsRequest request,
        CancellationToken cancellationToken)
    {
        await _remoteSettingsValidator.ValidateAndThrowAsync(request, cancellationToken);
        TryGetAdminId(out var adminId);

        var settings = await _remoteSettingsService.UpsertByMacAsync(macAddress, request, adminId, cancellationToken);
        if (settings is null)
        {
            return NotFound(new AgentErrorResponse
            {
                Error = "DeviceNotFound",
                Message = $"No device found with MAC address '{macAddress.Trim()}'."
            });
        }

        return Ok(settings);
    }

    [HttpGet("devices/{macAddress}/agent-advanced-settings")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DeviceAgentAdvancedSettingsDto>> GetDeviceAgentAdvancedSettings(
        string macAddress,
        CancellationToken cancellationToken)
    {
        var settings = await _advancedSettingsService.GetByMacAsync(macAddress, cancellationToken);
        if (settings is null)
        {
            return NotFound(new AgentErrorResponse
            {
                Error = "DeviceNotFound",
                Message = $"No device found with MAC address '{macAddress.Trim()}'."
            });
        }

        return Ok(settings);
    }

    [HttpPut("devices/{macAddress}/agent-advanced-settings")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DeviceAgentAdvancedSettingsDto>> UpsertDeviceAgentAdvancedSettings(
        string macAddress,
        [FromBody] UpsertDeviceAgentAdvancedSettingsRequest request,
        CancellationToken cancellationToken)
    {
        await _advancedSettingsValidator.ValidateAndThrowAsync(request, cancellationToken);
        TryGetAdminId(out var adminId);

        var settings = await _advancedSettingsService.UpsertByMacAsync(macAddress, request, adminId, cancellationToken);
        if (settings is null)
        {
            return NotFound(new AgentErrorResponse
            {
                Error = "DeviceNotFound",
                Message = $"No device found with MAC address '{macAddress.Trim()}'."
            });
        }

        return Ok(settings);
    }

    [HttpPatch("devices/{macAddress}/remote-settings/inheritance")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DeviceRemoteSettingsDto>> PatchDeviceSettingsInheritance(
        string macAddress,
        [FromBody] PatchDeviceSettingsInheritanceRequest request,
        CancellationToken cancellationToken)
    {
        await _inheritanceValidator.ValidateAndThrowAsync(request, cancellationToken);

        var settings = await _remoteSettingsService.PatchInheritanceAsync(macAddress, request, cancellationToken);
        if (settings is null)
        {
            return NotFound(new AgentErrorResponse
            {
                Error = "DeviceNotFound",
                Message = $"No device found with MAC address '{macAddress.Trim()}'."
            });
        }

        return Ok(settings);
    }

    [HttpGet("devices/{macAddress}/effective-settings")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<EffectiveDeviceSettingsDto>> GetEffectiveDeviceSettings(
        string macAddress,
        CancellationToken cancellationToken)
    {
        var settings = await _effectiveSettingsResolver.ResolveEffectiveCombinedByMacAsync(macAddress, cancellationToken);
        if (settings is null)
        {
            return NotFound(new AgentErrorResponse
            {
                Error = "DeviceNotFound",
                Message = $"No device found with MAC address '{macAddress.Trim()}'."
            });
        }

        return Ok(settings);
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
