using FluentValidation;
using Intellinode.Application.Validators;
using Microsoft.Extensions.DependencyInjection;

namespace Intellinode.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<AgentClientStatusRequestValidator>(
            filter: scan => scan.ValidatorType != typeof(SystemSettingExecutionRequestValidator));
        return services;
    }
}
