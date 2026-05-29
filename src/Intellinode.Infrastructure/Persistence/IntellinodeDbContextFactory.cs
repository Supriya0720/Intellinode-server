using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Intellinode.Infrastructure.Persistence;

public sealed class IntellinodeDbContextFactory : IDesignTimeDbContextFactory<IntellinodeDbContext>
{
    public IntellinodeDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../Intellinode.Api"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<IntellinodeDbContext>();
        IntellinodeNpgsqlConfiguration.ConfigureDbContextOptions(
            optionsBuilder,
            configuration.GetConnectionString("DefaultConnection")!);

        return new IntellinodeDbContext(optionsBuilder.Options);
    }
}
