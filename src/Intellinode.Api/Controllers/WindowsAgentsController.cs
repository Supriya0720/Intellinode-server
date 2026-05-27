using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Intellinode.Api.Controllers;

[ApiController]
[Route("api/v1/agents/windows")]
public sealed class WindowsAgentsController : ControllerBase
{
    private readonly IWindowsAgentEnrollmentService _enrollmentService;
    private readonly IValidator<WindowsAgentEnrollRequest> _enrollValidator;
    private readonly IValidator<WindowsAgentRegisterRequest> _registerValidator;

    public WindowsAgentsController(
        IWindowsAgentEnrollmentService enrollmentService,
        IValidator<WindowsAgentEnrollRequest> enrollValidator,
        IValidator<WindowsAgentRegisterRequest> registerValidator)
    {
        _enrollmentService = enrollmentService;
        _enrollValidator = enrollValidator;
        _registerValidator = registerValidator;
    }

    /// <summary>
    /// First-time Windows enrollment with an admin one-time token. Creates the device if missing (PendingInventory).
    /// </summary>
    [HttpPost("enroll")]
    [AllowAnonymous]
    public async Task<ActionResult<AgentAuthResponse>> Enroll(
        [FromBody] WindowsAgentEnrollRequest request,
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
    /// One-shot FusionX sendXPDataFirstTime: Windows enroll with inventory in a single call.
    /// Returns the same credentials as enroll after promoting the device to Active.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AgentAuthResponse>> Register(
        [FromBody] WindowsAgentRegisterRequest request,
        CancellationToken cancellationToken)
    {
        await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _enrollmentService.RegisterAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return StatusCode(
                StatusCodes.Status401Unauthorized,
                new AgentErrorResponse
                {
                    Error = result.ErrorCode ?? "InvalidEnrollmentToken",
                    Message = result.Message ?? "Registration failed."
                });
        }

        return Ok(result.AuthResponse);
    }
}
