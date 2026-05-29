using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Intellinode.Api.Http;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Intellinode.Api.Controllers;

[ApiController]
[Route("api/v1/admin/device-manager")]
[Authorize(Roles = "Admin")]
public sealed class AdminDeviceManagerController : ControllerBase
{
    private readonly IDeviceManagerService _deviceManagerService;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<AdminDeviceManagerController> _logger;

    public AdminDeviceManagerController(
        IDeviceManagerService deviceManagerService,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<AdminDeviceManagerController> logger)
    {
        _deviceManagerService = deviceManagerService;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
    }

    [HttpGet("tree")]
    public async Task<ActionResult<DeviceTreeResponse>> GetTree(
        [FromQuery] DeviceTreeQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _deviceManagerService.GetTreeAsync(query, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(GetTree), ex, cancellationToken);
        }
    }

    [HttpGet("groups/{groupId:guid}")]
    public async Task<ActionResult<DeviceManagerGroupInfoDto>> GetGroupInfo(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        try
        {
            var info = await _deviceManagerService.GetGroupInfoAsync(groupId, cancellationToken);
            if (info is null)
            {
                return NotFound(new AgentErrorResponse
                {
                    Error = "GroupNotFound",
                    Message = $"No group found with id '{groupId}'."
                });
            }

            return Ok(info);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(GetGroupInfo), ex, cancellationToken);
        }
    }

    [HttpGet("devices/{deviceId:guid}")]
    public async Task<ActionResult<DeviceManagerDeviceInfoDto>> GetDeviceInfo(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var info = await _deviceManagerService.GetDeviceInfoAsync(deviceId, cancellationToken);
            if (info is null)
            {
                return NotFound(new AgentErrorResponse
                {
                    Error = "DeviceNotFound",
                    Message = $"No device found with id '{deviceId}'."
                });
            }

            return Ok(info);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(GetDeviceInfo), ex, cancellationToken);
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
}
