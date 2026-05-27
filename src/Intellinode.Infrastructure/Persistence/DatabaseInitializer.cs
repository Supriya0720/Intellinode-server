using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Intellinode.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntellinodeDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IntellinodeDbContext>>();

        await MigrationBootstrapper.EnsureAppliedAsync(dbContext, logger, cancellationToken);

        if (!await dbContext.Tenants.AnyAsync(t => t.Id == TenantDefaults.DefaultTenantId, cancellationToken))
        {
            dbContext.Tenants.Add(new Tenant
            {
                Id = TenantDefaults.DefaultTenantId,
                Name = "Default",
                HostName = "localhost"
            });
        }

        if (!await dbContext.DeviceGroups.AnyAsync(
                g => g.Id == TenantDefaults.DefaultDeviceGroupId,
                cancellationToken))
        {
            dbContext.DeviceGroups.Add(new DeviceGroup
            {
                Id = TenantDefaults.DefaultDeviceGroupId,
                TenantId = TenantDefaults.DefaultTenantId,
                Name = "Root",
                IsDefault = true
            });
        }

        if (!await dbContext.AdminUsers.AnyAsync(cancellationToken))
        {
            dbContext.AdminUsers.Add(new AdminUser
            {
                UserName = "admin",
                DisplayName = "System Administrator",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123")
            });
        }

        await SeedTenantAgentDefaultsAsync(scope.ServiceProvider, dbContext, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Intellinode database initialized.");
    }

    private static async Task SeedTenantAgentDefaultsAsync(
        IServiceProvider serviceProvider,
        IntellinodeDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var agentOptions = serviceProvider.GetRequiredService<IOptions<AgentServerOptions>>().Value;
        var serverBaseUrl = agentOptions.ServerBaseUrl.TrimEnd('/');
        var apiBaseUrl = string.IsNullOrWhiteSpace(agentOptions.ApiBaseUrl)
            ? $"{serverBaseUrl}/api/v1"
            : agentOptions.ApiBaseUrl.TrimEnd('/');

        var defaults = await dbContext.TenantAgentDefaults
            .FirstOrDefaultAsync(t => t.TenantId == TenantDefaults.DefaultTenantId, cancellationToken);

        if (defaults is null)
        {
            dbContext.TenantAgentDefaults.Add(new TenantAgentDefaults
            {
                TenantId = TenantDefaults.DefaultTenantId,
                ServerBaseUrl = serverBaseUrl,
                ApiBaseUrl = apiBaseUrl,
                DefaultPollIntervalSeconds = agentOptions.DefaultPollIntervalSeconds,
                DefaultCommunicationType = CommunicationType.HTTPS,
                MinPollIntervalHttp = 30,
                UpdatedUtc = DateTime.UtcNow
            });
            return;
        }

        defaults.ServerBaseUrl = serverBaseUrl;
        defaults.ApiBaseUrl = apiBaseUrl;
        defaults.DefaultPollIntervalSeconds = agentOptions.DefaultPollIntervalSeconds;
        defaults.UpdatedUtc = DateTime.UtcNow;
    }
}
