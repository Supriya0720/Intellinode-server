using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Tests.Keyboard;

public sealed class DeviceKeyboardSettingsModelTests
{
    [Fact]
    public void SettingsKind_IncludesKeyboard()
    {
        Assert.Equal(2, (int)SettingsKind.Keyboard);
        Assert.True(Enum.IsDefined(typeof(SettingsKind), SettingsKind.Keyboard));
    }

    [Fact]
    public void DbContext_Model_IncludesDeviceKeyboardSettings()
    {
        var options = new DbContextOptionsBuilder<IntellinodeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new IntellinodeDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(Domain.Entities.DeviceKeyboardSettings));

        Assert.NotNull(entityType);
        Assert.Equal("device_keyboard_settings", entityType.GetTableName());
        Assert.Contains(
            context.Model.GetEntityTypes(),
            t => t.ClrType == typeof(Domain.Entities.DeviceSettingsApplyLog)
                 && t.FindProperty(nameof(Domain.Entities.DeviceSettingsApplyLog.TaskId)) is not null);
    }
}
