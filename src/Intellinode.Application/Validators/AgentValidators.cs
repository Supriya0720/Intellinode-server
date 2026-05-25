using FluentValidation;
using Intellinode.Application.Contracts.Agents;

namespace Intellinode.Application.Validators;

public sealed class AgentClientStatusRequestValidator : AbstractValidator<AgentClientStatusRequest>
{
    public AgentClientStatusRequestValidator()
    {
        RuleFor(x => x.MacAddress).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ClientStatus).NotEmpty().MaximumLength(50);
        RuleFor(x => x.IpAddress).NotEmpty().MaximumLength(512);
    }
}

public sealed class AgentAuthRequestValidator : AbstractValidator<AgentAuthRequest>
{
    public AgentAuthRequestValidator()
    {
        RuleFor(x => x.DeviceIdentity).NotEmpty().MaximumLength(300);
    }
}

public sealed class AgentRefreshRequestValidator : AbstractValidator<AgentRefreshRequest>
{
    public AgentRefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public sealed class AgentRevokeRequestValidator : AbstractValidator<AgentRevokeRequest>
{
    public AgentRevokeRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public sealed class AdminLoginRequestValidator : AbstractValidator<AdminLoginRequest>
{
    public AdminLoginRequestValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}

public sealed class AgentEnrollRequestValidator : AbstractValidator<AgentEnrollRequest>
{
    public AgentEnrollRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(512);
        RuleFor(x => x.DeviceIdentity).MaximumLength(300);
    }
}

public sealed class AgentInventoryRequestValidator : AbstractValidator<AgentInventoryRequest>
{
    public AgentInventoryRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Hardware.HasValue || x.Network.HasValue || x.OsInfo.HasValue || x.Security.HasValue)
            .WithMessage("At least one inventory section is required.");
    }
}

public sealed class AgentTaskAckRequestValidator : AbstractValidator<AgentTaskAckRequest>
{
    public AgentTaskAckRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.TaskId is Guid taskId && taskId != Guid.Empty || x.LegacyTaskId is int id && id > 0)
            .WithMessage("Either TaskId or LegacyTaskId must be provided.");

        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => s is "Completed" or "Failed")
            .WithMessage("Status must be 'Completed' or 'Failed'.");

        RuleFor(x => x.AckCode).MaximumLength(32);
    }
}

public sealed class AgentTaskAckBatchRequestValidator : AbstractValidator<AgentTaskAckBatchRequest>
{
    public AgentTaskAckBatchRequestValidator()
    {
        RuleFor(x => x.Acknowledgements)
            .NotEmpty()
            .WithMessage("At least one acknowledgement is required.");

        RuleForEach(x => x.Acknowledgements).SetValidator(new AgentTaskAckRequestValidator());
    }
}

public sealed class AdminQueueTaskRequestValidator : AbstractValidator<AdminQueueTaskRequest>
{
    public AdminQueueTaskRequestValidator()
    {
        RuleFor(x => x.ModuleName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.FunctionName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.FunctionParameter).MaximumLength(512);
        RuleFor(x => x.LegacyTaskId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Signal).MaximumLength(512);
        RuleFor(x => x.ExtraData).MaximumLength(512);
    }
}