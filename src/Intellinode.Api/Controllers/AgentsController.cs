using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Intellinode.Api.Controllers;

[ApiController]
[Route("api/v1/agents")]
public sealed class AgentsController : ControllerBase
{
    private readonly IAgentAuthService _agentAuthService;
    private readonly IAgentBootstrapService _bootstrapService;
    private readonly IAgentEnrollmentService _enrollmentService;
    private readonly IAgentInventoryService _inventoryService;
    private readonly IHeartbeatService _heartbeatService;
    private readonly IAgentTaskService _agentTaskService;
    private readonly IValidator<AgentAuthRequest> _authValidator;
    private readonly IValidator<AgentRefreshRequest> _refreshValidator;
    private readonly IValidator<AgentRevokeRequest> _revokeValidator;
    private readonly IValidator<AgentClientStatusRequest> _heartbeatValidator;
    private readonly IValidator<AgentEnrollRequest> _enrollValidator;
    private readonly IValidator<AgentInventoryRequest> _inventoryValidator;
    private readonly IValidator<AgentTaskAckBatchRequest> _taskAckValidator;

    public AgentsController(
        IAgentAuthService agentAuthService,
        IAgentBootstrapService bootstrapService,
        IAgentEnrollmentService enrollmentService,
        IAgentInventoryService inventoryService,
        IHeartbeatService heartbeatService,
        IAgentTaskService agentTaskService,
        IValidator<AgentAuthRequest> authValidator,
        IValidator<AgentRefreshRequest> refreshValidator,
        IValidator<AgentRevokeRequest> revokeValidator,
        IValidator<AgentClientStatusRequest> heartbeatValidator,
        IValidator<AgentEnrollRequest> enrollValidator,
        IValidator<AgentInventoryRequest> inventoryValidator,
        IValidator<AgentTaskAckBatchRequest> taskAckValidator)
    {
        _agentAuthService = agentAuthService;
        _bootstrapService = bootstrapService;
        _enrollmentService = enrollmentService;
        _inventoryService = inventoryService;
        _heartbeatService = heartbeatService;
        _agentTaskService = agentTaskService;
        _authValidator = authValidator;
        _refreshValidator = refreshValidator;
        _revokeValidator = revokeValidator;
        _heartbeatValidator = heartbeatValidator;
        _enrollValidator = enrollValidator;
        _inventoryValidator = inventoryValidator;
        _taskAckValidator = taskAckValidator;
    }

    /// <summary>
    /// Returns server and API base URLs plus agent endpoint paths (FusionX WebServerAddress provisioning).
    /// </summary>
    [HttpGet("bootstrap")]
    [AllowAnonymous]
    public ActionResult<AgentBootstrapResponse> Bootstrap() =>
        Ok(_bootstrapService.GetBootstrap());

    /// <summary>
    /// Bootstrap/reconnect for a known MAC address. Creates the device if missing (PendingInventory).
    /// Does not consume enrollment tokens; use enroll for first-time provisioning with an admin one-time token.
    /// </summary>
    [HttpPost("auth/token")]
    [AllowAnonymous]
    public async Task<ActionResult<AgentAuthResponse>> Authenticate(
        [FromBody] AgentAuthRequest request,
        CancellationToken cancellationToken)
    {
        await _authValidator.ValidateAndThrowAsync(request, cancellationToken);
        var response = await _agentAuthService.AuthenticateAsync(request, cancellationToken);
        return Ok(response);
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

    /// <summary>
    /// Revokes a refresh token (e.g. agent uninstall or logout). Returns 204 even when the token is unknown.
    /// </summary>
    [HttpPost("auth/revoke")]
    [AllowAnonymous]
    public async Task<IActionResult> RevokeRefreshToken(
        [FromBody] AgentRevokeRequest request,
        CancellationToken cancellationToken)
    {
        await _revokeValidator.ValidateAndThrowAsync(request, cancellationToken);
        await _agentAuthService.RevokeRefreshTokenAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// First-time enrollment with an admin one-time token. Creates the device if missing (PendingInventory).
    /// </summary>
    [HttpPost("enroll")]
    [AllowAnonymous]
    public async Task<ActionResult<AgentAuthResponse>> Enroll(
        [FromBody] AgentEnrollRequest request,
        CancellationToken cancellationToken)
    {
        await _enrollValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _enrollmentService.EnrollAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return StatusCode(
                StatusCodes.Status401Unauthorized,
                new AgentErrorResponse
                {
                    Error = result.ErrorCode ?? "InvalidEnrollmentToken",
                    Message = result.Message ?? "Enrollment failed."
                });
        }

        return Ok(result.AuthResponse);
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
        await _inventoryValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (!TryGetDeviceId(out var deviceId))
        {
            return Unauthorized();
        }

        await _inventoryService.UpsertInventoryAsync(deviceId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Replaces legacy SendXP_Client_Message heartbeat SOAP call.
    /// </summary>
    [HttpPost("heartbeat")]
    [Authorize(Roles = "Agent")]
    public async Task<ActionResult<HeartbeatResponse>> SendHeartbeat(
        [FromBody] AgentClientStatusRequest request,
        CancellationToken cancellationToken)
    {
        await _heartbeatValidator.ValidateAndThrowAsync(request, cancellationToken);

        var tokenMac = User.FindFirst("mac")?.Value;
        if (!string.IsNullOrWhiteSpace(tokenMac) &&
            !string.Equals(tokenMac, request.MacAddress.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var response = await _heartbeatService.ProcessHeartbeatAsync(request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Replaces legacy getXPPOllData — agent fetches pending work when autoDiscoverFlag is "1".
    /// </summary>
    [HttpGet("tasks/pending")]
    [Authorize(Roles = "Agent")]
    public async Task<ActionResult<AgentPendingTasksResponse>> GetPendingTasks(CancellationToken cancellationToken)
    {
        if (!TryGetDeviceId(out var deviceId))
        {
            return Unauthorized();
        }

        var response = await _agentTaskService.GetPendingTasksAsync(deviceId, cancellationToken);
        return Ok(response);
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
        await _taskAckValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (!TryGetDeviceId(out var deviceId))
        {
            return Unauthorized();
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

    private bool TryGetDeviceId(out Guid deviceId)
    {
        deviceId = default;
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
                      User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      User.FindFirstValue("sub");

        return !string.IsNullOrWhiteSpace(subject) && Guid.TryParse(subject, out deviceId);
    }
}
