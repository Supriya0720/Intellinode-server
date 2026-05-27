using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsAgentEnrollmentService : IWindowsAgentEnrollmentService
{
    private const string WindowsOs = "Windows";

    private readonly IntellinodeDbContext _dbContext;
    private readonly EnrollmentCoreService _enrollmentCore;
    private readonly AgentCredentialIssuer _credentialIssuer;
    private readonly IAgentInventoryService _inventoryService;

    public WindowsAgentEnrollmentService(
        IntellinodeDbContext dbContext,
        EnrollmentCoreService enrollmentCore,
        AgentCredentialIssuer credentialIssuer,
        IAgentInventoryService inventoryService)
    {
        _dbContext = dbContext;
        _enrollmentCore = enrollmentCore;
        _credentialIssuer = credentialIssuer;
        _inventoryService = inventoryService;
    }

    public async Task<AgentEnrollResult> EnrollAsync(
        WindowsAgentEnrollRequest request,
        CancellationToken cancellationToken = default)
    {
        var enrollment = await _enrollmentCore.FindValidEnrollmentTokenAsync(request.Token, cancellationToken);
        if (enrollment is null)
        {
            return AgentEnrollResult.Failure(
                "InvalidEnrollmentToken",
                "The enrollment token is invalid, expired, or has already been used.");
        }

        var platformError = EnrollmentCoreService.ValidatePlatform(enrollment, AgentPlatform.Windows);
        if (platformError is not null)
        {
            return platformError;
        }

        var (macAddress, macError) = EnrollmentCoreService.ResolveMacAddress(request.DeviceIdentity, enrollment);
        if (macAddress is null)
        {
            return macError == "MacMismatch"
                ? AgentEnrollResult.Failure(
                    "MacMismatch",
                    "The device identity does not match the MAC address bound to this enrollment token.")
                : AgentEnrollResult.Failure(
                    "InvalidEnrollmentToken",
                    "A valid device identity is required to complete enrollment.");
        }

        var (device, isNew) = await _enrollmentCore.UpsertDeviceForEnrollmentAsync(
            macAddress,
            WindowsOs,
            cancellationToken);
        if (isNew)
        {
            device.EnrollmentState = EnrollmentState.PendingInventory;
        }

        EnrollmentCoreService.CompleteEnrollment(enrollment, device);

        var response = await _credentialIssuer.IssueAgentCredentialsAsync(device, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return AgentEnrollResult.Success(response);
    }

    public async Task<AgentEnrollResult> RegisterAsync(
        WindowsAgentRegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var enrollment = await _enrollmentCore.FindValidEnrollmentTokenAsync(request.Token, cancellationToken);
        if (enrollment is null)
        {
            return AgentEnrollResult.Failure(
                "InvalidEnrollmentToken",
                "The enrollment token is invalid, expired, or has already been used.");
        }

        var platformError = EnrollmentCoreService.ValidatePlatform(enrollment, AgentPlatform.Windows);
        if (platformError is not null)
        {
            return platformError;
        }

        var (macAddress, macError) = EnrollmentCoreService.ResolveMacAddress(request.DeviceIdentity, enrollment);
        if (macAddress is null)
        {
            return macError == "MacMismatch"
                ? AgentEnrollResult.Failure(
                    "MacMismatch",
                    "The device identity does not match the MAC address bound to this enrollment token.")
                : AgentEnrollResult.Failure(
                    "InvalidEnrollmentToken",
                    "A valid device identity is required to complete enrollment.");
        }

        var (device, isNew) = await _enrollmentCore.UpsertDeviceForEnrollmentAsync(
            macAddress,
            WindowsOs,
            cancellationToken);

        var inventory = request.Inventory.ToAgentInventoryRequest();

        if (!isNew &&
            (device.EnrollmentState == EnrollmentState.Active ||
             await _enrollmentCore.DeviceHasInventoryAsync(device.Id, cancellationToken)))
        {
            await _inventoryService.ApplyInventoryAsync(device.Id, inventory, cancellationToken);
            var resyncResponse = await _credentialIssuer.IssueAgentCredentialsAsync(device, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return AgentEnrollResult.Success(resyncResponse);
        }

        await _inventoryService.ApplyInventoryAsync(device.Id, inventory, cancellationToken);
        EnrollmentCoreService.CompleteEnrollment(enrollment, device);

        var response = await _credentialIssuer.IssueAgentCredentialsAsync(device, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return AgentEnrollResult.Success(response);
    }
}
