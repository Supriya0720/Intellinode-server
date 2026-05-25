using FluentValidation;
using Intellinode.Application.Interfaces;
using Intellinode.Application.Validators;
using Microsoft.Extensions.DependencyInjection;

namespace Intellinode.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<AgentClientStatusRequestValidator>();
        return services;
    }
}
