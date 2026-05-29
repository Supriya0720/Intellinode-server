using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Intellinode.Api.Http;
using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Options;
using Intellinode.Infrastructure.Persistence;
using Intellinode.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intellinode.Api.Controllers;

[ApiController]
[Route("api/v1/agents")]
public sealed class AgentsController : ControllerBase
{
    private readonly IAgentAuthService _agentAuthService;
    private readonly IAgentBootstrapService _bootstrapService;
    private readonly IAgentInventoryService _inventoryService;
    private readonly IHeartbeatService _heartbeatService;
    private readonly IAgentTaskService _agentTaskService;
    private readonly IDeviceRemoteSettingsService _remoteSettingsService;
    private readonly IEffectiveAgentSettingsResolver _effectiveSettingsResolver;
    private readonly IntellinodeDbContext _dbContext;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<AgentsController> _logger;
    private readonly IValidator<AgentAuthRequest> _authValidator;
    private readonly IValidator<AgentRefreshRequest> _refreshValidator;
    private readonly IValidator<AgentRevokeRequest> _revokeValidator;
    private readonly IValidator<AgentClientStatusRequest> _heartbeatValidator;
    private readonly IValidator<AgentInventoryRequest> _inventoryValidator;
    private readonly IValidator<AgentTaskAckBatchRequest> _taskAckValidator;
    private readonly IValidator<AgentConfigAckRequest> _configAckValidator;
    private readonly AgentDiscoveryOptions _agentDiscoveryOptions;

    public AgentsController(
        IAgentAuthService agentAuthService,
        IAgentBootstrapService bootstrapService,
        IAgentInventoryService inventoryService,
        IHeartbeatService heartbeatService,
        IAgentTaskService agentTaskService,
        IDeviceRemoteSettingsService remoteSettingsService,
        IEffectiveAgentSettingsResolver effectiveSettingsResolver,
        IntellinodeDbContext dbContext,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<AgentsController> logger,
        IValidator<AgentAuthRequest> authValidator,
        IValidator<AgentRefreshRequest> refreshValidator,
        IValidator<AgentRevokeRequest> revokeValidator,
        IValidator<AgentClientStatusRequest> heartbeatValidator,
        IValidator<AgentInventoryRequest> inventoryValidator,
        IValidator<AgentTaskAckBatchRequest> taskAckValidator,
        IValidator<AgentConfigAckRequest> configAckValidator,
        IOptions<AgentDiscoveryOptions> agentDiscoveryOptions)
    {
        _agentAuthService = agentAuthService;
        _bootstrapService = bootstrapService;
        _inventoryService = inventoryService;
        _heartbeatService = heartbeatService;
        _agentTaskService = agentTaskService;
        _remoteSettingsService = remoteSettingsService;
        _effectiveSettingsResolver = effectiveSettingsResolver;
        _dbContext = dbContext;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
        _authValidator = authValidator;
        _refreshValidator = refreshValidator;
        _revokeValidator = revokeValidator;
        _heartbeatValidator = heartbeatValidator;
        _inventoryValidator = inventoryValidator;
        _taskAckValidator = taskAckValidator;
        _configAckValidator = configAckValidator;
        _agentDiscoveryOptions = agentDiscoveryOptions.Value;
    }

    /// <summary>
    /// Returns server and API base URLs plus agent endpoint paths (FusionX WebServerAddress provisioning).
    /// </summary>
    [HttpGet("bootstrap")]
    [AllowAnonymous]
    public async Task<ActionResult<AgentBootstrapResponse>> Bootstrap()
    {
        try
        {
            return Ok(_bootstrapService.GetBootstrap());
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(Bootstrap), ex);
        }
    }

    /// <summary>
    /// Returns effective remote agent settings for the authenticated device (desired config pull).
    /// Poll interval and server URLs reflect per-device settings or tenant defaults.
    /// </summary>
    [HttpGet("config")]
    [Authorize(Roles = "Agent")]
    public async Task<ActionResult<AgentConfigResponse>> GetConfig(CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetDeviceId(out var deviceId))
            {
                return Unauthorized();
            }

            var accessBlock = await GetAgentAccessBlockAsync(deviceId, cancellationToken);
            if (accessBlock is not null)
            {
                return accessBlock;
            }

            var macAddress = User.FindFirst("mac")?.Value;
            if (string.IsNullOrWhiteSpace(macAddress))
            {
                return Unauthorized();
            }

            var config = await _remoteSettingsService.GetAgentConfigAsync(macAddress, cancellationToken);
            if (config is null)
            {
                return NotFound(new AgentErrorResponse
                {
                    Error = "DeviceNotFound",
                    Message = "Device associated with this token was not found."
                });
            }

            return Ok(config);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(GetConfig), ex, cancellationToken);
        }
    }

    /// <summary>
    /// Agent confirms applied general and/or advanced config versions.
    /// </summary>
    [HttpPost("config/ack")]
    [Authorize(Roles = "Agent")]
    public async Task<ActionResult<AgentConfigAckResponse>> AcknowledgeConfig(
        [FromBody] AgentConfigAckRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _configAckValidator.ValidateAndThrowAsync(request, cancellationToken);

            if (!TryGetDeviceId(out var deviceId))
            {
                return Unauthorized();
            }

            var result = await _effectiveSettingsResolver.AcknowledgeConfigAsync(deviceId, request, cancellationToken);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(AcknowledgeConfig), ex, cancellationToken);
        }
    }

    /// <summary>
    /// Bootstrap/reconnect for a known MAC address. Creates the device if missing (PendingInventory).
    /// Does not consume enrollment tokens; use POST /api/v1/agents/windows/enroll for first-time Windows provisioning.
    /// </summary>
    [HttpPost("auth/token")]
    [AllowAnonymous]
    public async Task<ActionResult<AgentAuthResponse>> Authenticate(
        [FromBody] AgentAuthRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _authValidator.ValidateAndThrowAsync(request, cancellationToken);
            var response = await _agentAuthService.AuthenticateAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(Authenticate), ex, cancellationToken);
        }
    }

    /// <summary>
    /// Exchanges a refresh token for a new access/refresh JWT pair without re-enrolling.
    /// Does not change enrollment state, re-consume enrollment tokens, or create new devices.
    /// Clients must persist the new refreshToken from the response after each successful refresh.
    /// </summary>
    [HttpPost("auth/refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AgentAuthResponse>> Refresh(
        [FromBody] AgentRefreshRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _refreshValidator.ValidateAndThrowAsync(request, cancellationToken);
            var result = await _agentAuthService.RefreshAsync(request, cancellationToken);
            if (!result.IsSuccess)
            {
                return StatusCode(
                    StatusCodes.Status401Unauthorized,
                    new AgentErrorResponse
                    {
                        Error = result.ErrorCode ?? "InvalidRefreshToken",
                        Message = result.Message ?? "Refresh failed."
                    });
            }

            return Ok(result.AuthResponse);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(Refresh), ex, cancellationToken);
        }
    }

    /// <summary>
    /// Revokes a refresh token (e.g. agent uninstall or logout). Returns 204 even when the token is unknown.
    /// </summary>
    [HttpPost("auth/revoke")]
    [AllowAnonymous]
    public async Task<IActionResult> RevokeRefreshToken(
        [FromBody] AgentRevokeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _revokeValidator.ValidateAndThrowAsync(request, cancellationToken);
            await _agentAuthService.RevokeRefreshTokenAsync(request, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(RevokeRefreshToken), ex, cancellationToken);
        }
    }

    /// <summary>
    /// Uploads full device inventory after SDFT.
    /// </summary>
    [HttpPost("inventory")]
    [Authorize(Roles = "Agent")]
    public async Task<IActionResult> UploadInventory(
        [FromBody] AgentInventoryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _inventoryValidator.ValidateAndThrowAsync(request, cancellationToken);

            if (!TryGetDeviceId(out var deviceId))
            {
                return Unauthorized();
            }

            var result = await _inventoryService.UpsertInventoryAsync(
                deviceId,
                request,
                InventorySubmissionKind.SelfDiscovery,
                cancellationToken);

            if (!result.IsSuccess)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new AgentErrorResponse
                    {
                        Error = result.ErrorCode ?? "DeviceRejected",
                        Message = result.Message
                    });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(UploadInventory), ex, cancellationToken);
        }
    }

    /// <summary>
    /// Replaces legacy SendXP_Client_Message heartbeat SOAP call.
    /// FusionX agents may request legacy plain-text via Accept: text/plain or ?format=legacy;
    /// the response body is then only the autoDiscoverFlag wire value ("0", "1", "SDFT", "2", etc.).
    /// When <see cref="AgentDiscoveryOptions.LegacyMapExistsToTwo"/> is true, internal "exists" maps to "2" on the legacy path only; JSON responses always use "exists".
    /// </summary>
    [HttpPost("heartbeat")]
    [Authorize(Roles = "Agent")]
    public async Task<IActionResult> SendHeartbeat(
        [FromBody] AgentClientStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _heartbeatValidator.ValidateAndThrowAsync(request, cancellationToken);

            var tokenMac = User.FindFirst("mac")?.Value;
            if (!string.IsNullOrWhiteSpace(tokenMac) &&
                !string.Equals(tokenMac, request.MacAddress.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var response = await _heartbeatService.ProcessHeartbeatAsync(request, cancellationToken);
            if (WantsLegacyHeartbeatResponse())
            {
                var wireFlag = LegacyHeartbeatFlagMapper.ToWireFormat(
                    response.AutoDiscoverFlag,
                    _agentDiscoveryOptions.LegacyMapExistsToTwo);
                return Content(wireFlag, "text/plain");
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(SendHeartbeat), ex, cancellationToken);
        }
    }

    private bool WantsLegacyHeartbeatResponse()
    {
        var format = Request.Query["format"].ToString();
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(format, "legacy", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var accept = Request.Headers.Accept.ToString();
        return accept.Contains("text/plain", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Replaces legacy getXPPOllData — agent fetches pending work when autoDiscoverFlag is "1".
    /// </summary>
    [HttpGet("tasks/pending")]
    [Authorize(Roles = "Agent")]
    public async Task<ActionResult<AgentPendingTasksResponse>> GetPendingTasks(CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetDeviceId(out var deviceId))
            {
                return Unauthorized();
            }

            var accessBlock = await GetAgentAccessBlockAsync(deviceId, cancellationToken);
            if (accessBlock is not null)
            {
                return accessBlock;
            }

            var response = await _agentTaskService.GetPendingTasksAsync(deviceId, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(GetPendingTasks), ex, cancellationToken);
        }
    }

    /// <summary>
    /// Replaces legacy getXPAckNEW — agent acknowledges completed or failed tasks (batch body).
    /// </summary>
    [HttpPost("tasks/ack")]
    [Authorize(Roles = "Agent")]
    public async Task<IActionResult> AcknowledgeTasks(
        [FromBody] AgentTaskAckBatchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _taskAckValidator.ValidateAndThrowAsync(request, cancellationToken);

            if (!TryGetDeviceId(out var deviceId))
            {
                return Unauthorized();
            }

            var accessBlock = await GetAgentAccessBlockAsync(deviceId, cancellationToken);
            if (accessBlock is not null)
            {
                return accessBlock;
            }

            try
            {
                await _agentTaskService.AcknowledgeTasksAsync(deviceId, request, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new AgentErrorResponse
                {
                    Error = "TaskAckFailed",
                    Message = ex.Message
                });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(AcknowledgeTasks), ex, cancellationToken);
        }
    }

    private async Task<ObjectResult> HandleUnexpectedExceptionAsync(
        string actionName,
        Exception ex,
        CancellationToken cancellationToken = default)
    {
        Guid? deviceId = TryGetDeviceId(out var id) ? id : null;
        return await this.HandleUnexpectedExceptionAsync(
            _exceptionLogWriter,
            _logger,
            actionName,
            ex,
            deviceId,
            cancellationToken: cancellationToken);
    }

    private bool TryGetDeviceId(out Guid deviceId)
    {
        deviceId = default;
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
                      User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      User.FindFirstValue("sub");

        return !string.IsNullOrWhiteSpace(subject) && Guid.TryParse(subject, out deviceId);
    }

    private async Task<ActionResult?> GetAgentAccessBlockAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var enrollmentState = await _dbContext.Devices
            .AsNoTracking()
            .Where(d => d.Id == deviceId)
            .Select(d => (EnrollmentState?)d.EnrollmentState)
            .FirstOrDefaultAsync(cancellationToken);

        if (enrollmentState is null)
        {
            return NotFound(new AgentErrorResponse
            {
                Error = "DeviceNotFound",
                Message = "Device associated with this token was not found."
            });
        }

        var (blocked, errorCode, message) = DeviceEnrollmentGuard.GetAgentAccessBlock(enrollmentState.Value);
        if (!blocked)
        {
            return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, new AgentErrorResponse
        {
            Error = errorCode,
            Message = message
        });

    }
}
