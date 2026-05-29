using Intellinode.Api.Http;
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
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<WindowsAgentsController> _logger;
    private readonly IValidator<WindowsAgentEnrollRequest> _enrollValidator;
    private readonly IValidator<WindowsAgentRegisterRequest> _registerValidator;

    public WindowsAgentsController(
        IWindowsAgentEnrollmentService enrollmentService,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<WindowsAgentsController> logger,
        IValidator<WindowsAgentEnrollRequest> enrollValidator,
        IValidator<WindowsAgentRegisterRequest> registerValidator)
    {
        _enrollmentService = enrollmentService;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
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
        try
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
        catch (Exception ex)
        {
            return await this.HandleUnexpectedExceptionAsync(
                _exceptionLogWriter,
                _logger,
                nameof(Enroll),
                ex,
                cancellationToken: cancellationToken);
        }
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
        try
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
        catch (Exception ex)
        {
            return await this.HandleUnexpectedExceptionAsync(
                _exceptionLogWriter,
                _logger,
                nameof(Register),
                ex,
                cancellationToken: cancellationToken);
        }
    }
}
