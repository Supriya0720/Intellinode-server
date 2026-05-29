using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Intellinode.Api.Http;
using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Intellinode.Api.Controllers;

[ApiController]
[Route("api/v1/admin/groups")]
[Authorize(Roles = "Admin")]
public sealed class AdminGroupSettingsController : ControllerBase
{
    private readonly IGroupRemoteSettingsService _groupSettingsService;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<AdminGroupSettingsController> _logger;
    private readonly IValidator<UpsertGroupRemoteSettingsRequest> _groupRemoteValidator;
    private readonly IValidator<UpsertGroupAgentAdvancedSettingsRequest> _groupAdvancedValidator;

    public AdminGroupSettingsController(
        IGroupRemoteSettingsService groupSettingsService,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<AdminGroupSettingsController> logger,
        IValidator<UpsertGroupRemoteSettingsRequest> groupRemoteValidator,
        IValidator<UpsertGroupAgentAdvancedSettingsRequest> groupAdvancedValidator)
    {
        _groupSettingsService = groupSettingsService;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
        _groupRemoteValidator = groupRemoteValidator;
        _groupAdvancedValidator = groupAdvancedValidator;
    }

    [HttpGet("{groupId:guid}/remote-settings")]
    public async Task<ActionResult<GroupRemoteSettingsDto>> GetGroupRemoteSettings(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _groupSettingsService.GetGroupRemoteSettingsAsync(groupId, cancellationToken);
            return settings is null ? GroupNotFound(groupId) : Ok(settings);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(GetGroupRemoteSettings), ex, cancellationToken);
        }
    }

    [HttpPut("{groupId:guid}/remote-settings")]
    public async Task<ActionResult<GroupRemoteSettingsDto>> UpsertGroupRemoteSettings(
        Guid groupId,
        [FromBody] UpsertGroupRemoteSettingsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _groupRemoteValidator.ValidateAndThrowAsync(request, cancellationToken);
            TryGetAdminId(out var adminId);

            var settings = await _groupSettingsService.UpsertGroupRemoteSettingsAsync(groupId, request, adminId, cancellationToken);
            return settings is null ? GroupNotFound(groupId) : Ok(settings);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(UpsertGroupRemoteSettings), ex, cancellationToken);
        }
    }

    [HttpGet("{groupId:guid}/agent-advanced-settings")]
    public async Task<ActionResult<GroupAgentAdvancedSettingsDto>> GetGroupAdvancedSettings(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _groupSettingsService.GetGroupAdvancedSettingsAsync(groupId, cancellationToken);
            return settings is null ? GroupNotFound(groupId) : Ok(settings);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(GetGroupAdvancedSettings), ex, cancellationToken);
        }
    }

    [HttpPut("{groupId:guid}/agent-advanced-settings")]
    public async Task<ActionResult<GroupAgentAdvancedSettingsDto>> UpsertGroupAdvancedSettings(
        Guid groupId,
        [FromBody] UpsertGroupAgentAdvancedSettingsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _groupAdvancedValidator.ValidateAndThrowAsync(request, cancellationToken);
            TryGetAdminId(out var adminId);

            var settings = await _groupSettingsService.UpsertGroupAdvancedSettingsAsync(groupId, request, adminId, cancellationToken);
            return settings is null ? GroupNotFound(groupId) : Ok(settings);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(UpsertGroupAdvancedSettings), ex, cancellationToken);
        }
    }

    [HttpPost("{groupId:guid}/remote-settings/propagate")]
    public async Task<ActionResult<PropagateGroupSettingsResponse>> PropagateGroupSettings(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        try
        {
            TryGetAdminId(out var adminId);
            var result = await _groupSettingsService.PropagatePendingApplyAsync(groupId, adminId, cancellationToken);
            return result is null ? GroupNotFound(groupId) : Ok(result);
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedExceptionAsync(nameof(PropagateGroupSettings), ex, cancellationToken);
        }
    }

    private NotFoundObjectResult GroupNotFound(Guid groupId) =>
        NotFound(new AgentErrorResponse
        {
            Error = "GroupNotFound",
            Message = $"No group found with id '{groupId}' for tenant '{TenantDefaults.DefaultTenantId}'."
        });

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
