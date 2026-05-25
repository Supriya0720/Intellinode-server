using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Intellinode database initialized.");
    }
}
