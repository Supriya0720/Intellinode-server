using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IntellinodeDbContext))]
[Migration("20260525120000_AddAgentEnrollmentTokenPlatform")]
partial class AddAgentEnrollmentTokenPlatform
{
    /// <inheritdoc />
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
    }
}
