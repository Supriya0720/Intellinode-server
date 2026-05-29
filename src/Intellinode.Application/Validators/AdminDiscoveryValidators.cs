using FluentValidation;
using Intellinode.Application.Contracts.Admin;

namespace Intellinode.Application.Validators;

public sealed class ApproveDiscoveryRequestValidator : AbstractValidator<ApproveDiscoveryRequest>
{
    public ApproveDiscoveryRequestValidator()
    {
        RuleFor(x => x.HostName).MaximumLength(255);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.GroupId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("GroupId must be a valid GUID when provided.");
    }
}

public sealed class RejectDiscoveryRequestValidator : AbstractValidator<RejectDiscoveryRequest>
{
    public RejectDiscoveryRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class BulkApproveDiscoveryRequestValidator : AbstractValidator<BulkApproveDiscoveryRequest>
{
    public BulkApproveDiscoveryRequestValidator()
    {
        RuleFor(x => x.MacAddresses)
            .NotEmpty()
            .WithMessage("At least one MAC address is required.");

        RuleForEach(x => x.MacAddresses)
            .NotEmpty()
            .MaximumLength(300);
    }
}

public sealed class DismissDiscoveryRequestValidator : AbstractValidator<DismissDiscoveryRequest>
{
    public DismissDiscoveryRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
