using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Intellinode.Api.Http;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Intellinode.Api.Controllers;

[ApiController]
[Route("api/v1/admin/discover")]
[Authorize(Roles = "Admin")]
public sealed class AdminDiscoveryController : ControllerBase
{
    private readonly IDiscoverLookupService _discoverLookupService;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<AdminDiscoveryController> _logger;
    private readonly IValidator<ApproveDiscoveryRequest> _approveValidator;
    private readonly IValidator<RejectDiscoveryRequest> _rejectValidator;
    private readonly IValidator<BulkApproveDiscoveryRequest> _bulkApproveValidator;
    private readonly IValidator<DismissDiscoveryRequest> _dismissValidator;

    public AdminDiscoveryController(
        IDiscoverLookupService discoverLookupService,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<AdminDiscoveryController> logger,
        IValidator<ApproveDiscoveryRequest> approveValidator,
        IValidator<RejectDiscoveryRequest> rejectValidator,
        IValidator<BulkApproveDiscoveryRequest> bulkApproveValidator,
        IValidator<DismissDiscoveryRequest> dismissValidator)
    {
        _discoverLookupService = discoverLookupService;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
        _approveValidator = approveValidator;
        _rejectValidator = rejectValidator;
        _bulkApproveValidator = bulkApproveValidator;
        _dismissValidator = dismissValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedDiscoverLookupResponse>> List(
        [FromQuery] DiscoverLookupQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _discoverLookupService.ListAsync(query, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(List), ex, cancellationToken);
        }
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DiscoverLookupStatsResponse>> GetStats(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _discoverLookupService.GetStatsAsync(cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(GetStats), ex, cancellationToken);
        }
    }

    [HttpGet("{macAddress}")]
    public async Task<ActionResult<DiscoverLookupDetailDto>> GetByMac(
        string macAddress,
        CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _discoverLookupService.GetByMacAsync(macAddress, cancellationToken);
            if (detail is null)
            {
                return NotFound(new AgentErrorResponse
                {
                    Error = "DiscoveryNotFound",
                    Message = $"No discovery entry found for MAC address '{macAddress.Trim()}'."
                });
            }

            return Ok(detail);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(GetByMac), ex, cancellationToken);
        }
    }

    [HttpPost("{macAddress}/approve")]
    public async Task<ActionResult<ApproveDiscoveryResponse>> Approve(
        string macAddress,
        [FromBody] ApproveDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _approveValidator.ValidateAndThrowAsync(request, cancellationToken);

            if (!TryGetAdminId(out var adminId))
            {
                return Unauthorized();
            }

            var result = await _discoverLookupService.ApproveAsync(macAddress, adminId, request, cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return MapOperationResult(result);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(Approve), ex, cancellationToken);
        }
    }

    [HttpPost("{macAddress}/reject")]
    public async Task<IActionResult> Reject(
        string macAddress,
        [FromBody] RejectDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _rejectValidator.ValidateAndThrowAsync(request, cancellationToken);

            if (!TryGetAdminId(out var adminId))
            {
                return Unauthorized();
            }

            var result = await _discoverLookupService.RejectAsync(macAddress, adminId, request, cancellationToken);
            if (result.IsSuccess)
            {
                return NoContent();
            }

            return MapOperationResult(result);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(Reject), ex, cancellationToken);
        }
    }

    [HttpPost("bulk-approve")]
    public async Task<ActionResult<BulkApproveDiscoveryResponse>> BulkApprove(
        [FromBody] BulkApproveDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _bulkApproveValidator.ValidateAndThrowAsync(request, cancellationToken);

            if (!TryGetAdminId(out var adminId))
            {
                return Unauthorized();
            }

            var response = await _discoverLookupService.BulkApproveAsync(adminId, request, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(BulkApprove), ex, cancellationToken);
        }
    }

    [HttpDelete("{macAddress}")]
    public async Task<IActionResult> Dismiss(
        string macAddress,
        [FromBody] DismissDiscoveryRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            request ??= new DismissDiscoveryRequest();
            await _dismissValidator.ValidateAndThrowAsync(request, cancellationToken);

            if (!TryGetAdminId(out var adminId))
            {
                return Unauthorized();
            }

            var result = await _discoverLookupService.DismissAsync(macAddress, adminId, request, cancellationToken);
            if (result.IsSuccess)
            {
                return NoContent();
            }

            return MapOperationResult(result);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(Dismiss), ex, cancellationToken);
        }
    }

    private ActionResult MapOperationResult<T>(DiscoverLookupOperationResult<T> result)
    {
        return result.ErrorCode switch
        {
            "DiscoveryNotFound" => NotFound(new AgentErrorResponse
            {
                Error = result.ErrorCode,
                Message = result.Message ?? "Discovery entry not found."
            }),
            "DiscoveryAlreadyProcessed" or "InventoryMissing" => Conflict(new AgentErrorResponse
            {
                Error = result.ErrorCode,
                Message = result.Message ?? "Discovery operation could not be completed."
            }),
            "GroupNotFound" => NotFound(new AgentErrorResponse
            {
                Error = result.ErrorCode,
                Message = result.Message ?? "Device group not found."
            }),
            _ => BadRequest(new AgentErrorResponse
            {
                Error = result.ErrorCode ?? "DiscoveryOperationFailed",
                Message = result.Message ?? "Discovery operation failed."
            })
        };
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
