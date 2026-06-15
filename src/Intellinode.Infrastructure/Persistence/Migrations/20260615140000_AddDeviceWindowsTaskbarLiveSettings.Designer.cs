using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(IntellinodeDbContext))]
    [Migration("20260615140000_AddDeviceWindowsTaskbarLiveSettings")]
    partial class AddDeviceWindowsTaskbarLiveSettings
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            // Target model is maintained in IntellinodeDbContextModelSnapshot.
        }
    }
}
