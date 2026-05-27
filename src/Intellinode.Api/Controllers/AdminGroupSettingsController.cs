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
[Route("api/v1/admin/groups")]
[Authorize(Roles = "Admin")]
public sealed class AdminGroupSettingsController : ControllerBase
{
    private readonly IGroupRemoteSettingsService _groupSettingsService;
    private readonly IValidator<UpsertGroupRemoteSettingsRequest> _groupRemoteValidator;
    private readonly IValidator<UpsertGroupAgentAdvancedSettingsRequest> _groupAdvancedValidator;

    public AdminGroupSettingsController(
        IGroupRemoteSettingsService groupSettingsService,
        IValidator<UpsertGroupRemoteSettingsRequest> groupRemoteValidator,
        IValidator<UpsertGroupAgentAdvancedSettingsRequest> groupAdvancedValidator)
    {
        _groupSettingsService = groupSettingsService;
        _groupRemoteValidator = groupRemoteValidator;
        _groupAdvancedValidator = groupAdvancedValidator;
    }

    [HttpGet("{groupId:guid}/remote-settings")]
    public async Task<ActionResult<GroupRemoteSettingsDto>> GetGroupRemoteSettings(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var settings = await _groupSettingsService.GetGroupRemoteSettingsAsync(groupId, cancellationToken);
        return settings is null ? GroupNotFound(groupId) : Ok(settings);
    }

    [HttpPut("{groupId:guid}/remote-settings")]
    public async Task<ActionResult<GroupRemoteSettingsDto>> UpsertGroupRemoteSettings(
        Guid groupId,
        [FromBody] UpsertGroupRemoteSettingsRequest request,
        CancellationToken cancellationToken)
    {
        await _groupRemoteValidator.ValidateAndThrowAsync(request, cancellationToken);
        TryGetAdminId(out var adminId);

        var settings = await _groupSettingsService.UpsertGroupRemoteSettingsAsync(groupId, request, adminId, cancellationToken);
        return settings is null ? GroupNotFound(groupId) : Ok(settings);
    }

    [HttpGet("{groupId:guid}/agent-advanced-settings")]
    public async Task<ActionResult<GroupAgentAdvancedSettingsDto>> GetGroupAdvancedSettings(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var settings = await _groupSettingsService.GetGroupAdvancedSettingsAsync(groupId, cancellationToken);
        return settings is null ? GroupNotFound(groupId) : Ok(settings);
    }

    [HttpPut("{groupId:guid}/agent-advanced-settings")]
    public async Task<ActionResult<GroupAgentAdvancedSettingsDto>> UpsertGroupAdvancedSettings(
        Guid groupId,
        [FromBody] UpsertGroupAgentAdvancedSettingsRequest request,
        CancellationToken cancellationToken)
    {
        await _groupAdvancedValidator.ValidateAndThrowAsync(request, cancellationToken);
        TryGetAdminId(out var adminId);

        var settings = await _groupSettingsService.UpsertGroupAdvancedSettingsAsync(groupId, request, adminId, cancellationToken);
        return settings is null ? GroupNotFound(groupId) : Ok(settings);
    }

    [HttpPost("{groupId:guid}/remote-settings/propagate")]
    public async Task<ActionResult<PropagateGroupSettingsResponse>> PropagateGroupSettings(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        TryGetAdminId(out var adminId);
        var result = await _groupSettingsService.PropagatePendingApplyAsync(groupId, adminId, cancellationToken);
        return result is null ? GroupNotFound(groupId) : Ok(result);
    }

    private NotFoundObjectResult GroupNotFound(Guid groupId) =>
        NotFound(new AgentErrorResponse
        {
            Error = "GroupNotFound",
            Message = $"No group found with id '{groupId}' for tenant '{TenantDefaults.DefaultTenantId}'."
        });

    private bool TryGetAdminId(out Guid adminId)
    {
        adminId = default;
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
                      User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      User.FindFirstValue("sub");

        return !string.IsNullOrWhiteSpace(subject) && Guid.TryParse(subject, out adminId);
    }
}
