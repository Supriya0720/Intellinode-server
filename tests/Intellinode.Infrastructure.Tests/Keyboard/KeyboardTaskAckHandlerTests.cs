using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Options;
using Intellinode.Infrastructure.Persistence;
using Intellinode.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Intellinode.Infrastructure.Tests.Keyboard;

public sealed class KeyboardTaskAckHandlerTests
{
    [Fact]
    public async Task Ack_Completed_UpdatesAppliedState()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var (device, task) = SeedKeyboardApplyScenario(context, settingsVersion: 3);
        var handler = CreateHandler(context);

        task.Status = DeviceTaskStatus.Completed;
        task.CompletedUtc = DateTime.UtcNow;
        await handler.ApplyAckAsync(device, task, DeviceTaskStatus.Completed, reason: null, CancellationToken.None);
        await context.SaveChangesAsync();

        var keyboard = await context.DeviceKeyboardSettings.SingleAsync(k => k.DeviceId == device.Id);
        var logs = await context.DeviceSettingsApplyLogs
            .Where(l => l.DeviceId == device.Id)
            .OrderBy(l => l.CreatedUtc)
            .ToListAsync();

        Assert.Equal(DeviceTaskStatus.Completed, task.Status);
        Assert.Equal(3, keyboard.LastAppliedVersion);
        Assert.NotNull(keyboard.LastAppliedUtc);
        Assert.False(keyboard.PendingApply);
        Assert.Equal("Applied", keyboard.LastApplyStatus);
        Assert.Null(keyboard.LastApplyMessage);
        Assert.Equal(2, logs.Count);
        Assert.Equal(SettingsApplyStatus.Pending, logs[0].Status);
        Assert.Equal(SettingsApplyStatus.Applied, logs[1].Status);
        Assert.Equal(task.Id, logs[1].TaskId);
    }

    [Fact]
    public async Task Ack_Failed_StoresReason_DoesNotRevertDesiredSettings()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var (device, task) = SeedKeyboardApplyScenario(context, settingsVersion: 2, delay: 5, locale: "French (France)");
        var handler = CreateHandler(context);
        const string reason = "Failed to set keyboard layout: 0x80070057";

        task.Status = DeviceTaskStatus.Failed;
        task.CompletedUtc = DateTime.UtcNow;
        await handler.ApplyAckAsync(device, task, DeviceTaskStatus.Failed, reason, CancellationToken.None);
        await context.SaveChangesAsync();

        var keyboard = await context.DeviceKeyboardSettings.SingleAsync(k => k.DeviceId == device.Id);
        var failedLog = await context.DeviceSettingsApplyLogs
            .Where(l => l.DeviceId == device.Id && l.Status == SettingsApplyStatus.Failed)
            .SingleAsync();

        Assert.Equal(5, keyboard.Delay);
        Assert.Equal("French (France)", keyboard.KeyboardLocale);
        Assert.Equal(2, keyboard.SettingsVersion);
        Assert.Null(keyboard.LastAppliedVersion);
        Assert.False(keyboard.PendingApply);
        Assert.Equal("Failed", keyboard.LastApplyStatus);
        Assert.Equal(reason, keyboard.LastApplyMessage);
        Assert.Equal(reason, failedLog.Message);
    }

    [Fact]
    public void Ack_Failed_TruncatesReason_To500()
    {
        var longReason = new string('x', 600);
        var truncated = KeyboardTaskAckHandler.TruncateReason(longReason);

        Assert.NotNull(truncated);
        Assert.Equal(500, truncated!.Length);
        Assert.Equal(new string('x', 500), truncated);
    }

    [Fact]
    public async Task Ack_NonKeyboardTask_DoesNotTouchKeyboardSettings()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedDevice(context, "AA:BB:CC:DD:EE:99:XP");
        context.DeviceKeyboardSettings.Add(new DeviceKeyboardSettings
        {
            DeviceId = device.Id,
            Delay = 2,
            RepeatRate = 31,
            KeyboardLocale = "English (United States)",
            SettingsVersion = 1,
            PendingApply = true,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });
        var shutdownTask = new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = 1,
            ModuleName = "Power",
            FunctionName = "Shutdown",
            Status = DeviceTaskStatus.Pending,
            CreatedUtc = DateTime.UtcNow
        };
        context.DeviceTasks.Add(shutdownTask);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);
        await handler.ApplyAckAsync(device, shutdownTask, DeviceTaskStatus.Completed, null, CancellationToken.None);
        await context.SaveChangesAsync();

        var keyboard = await context.DeviceKeyboardSettings.SingleAsync(k => k.DeviceId == device.Id);
        Assert.True(keyboard.PendingApply);
        Assert.Null(keyboard.LastApplyStatus);
        Assert.Empty(await context.DeviceSettingsApplyLogs.Where(l => l.DeviceId == device.Id).ToListAsync());
    }

    private static IntellinodeDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<IntellinodeDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new IntellinodeDbContext(options);
    }

    private static KeyboardTaskAckHandler CreateHandler(IntellinodeDbContext context)
    {
        var resolver = new EffectiveAgentSettingsResolver(
            context,
            Microsoft.Extensions.Options.Options.Create(new AgentServerOptions
            {
                ServerBaseUrl = "https://localhost:5288",
                ApiBaseUrl = "https://localhost:5288/api/v1",
                DefaultPollIntervalSeconds = 300
            }));

        return new KeyboardTaskAckHandler(
            context,
            resolver,
            NullLogger<KeyboardTaskAckHandler>.Instance);
    }

    private static (Device Device, DeviceTask Task) SeedKeyboardApplyScenario(
        IntellinodeDbContext context,
        long settingsVersion,
        int delay = 2,
        string locale = "English (United States)")
    {
        var device = SeedDevice(context, "AA:BB:CC:DD:EE:60:XP");
        context.DeviceKeyboardSettings.Add(new DeviceKeyboardSettings
        {
            DeviceId = device.Id,
            Delay = delay,
            RepeatRate = 31,
            KeyboardLocale = locale,
            SettingsVersion = settingsVersion,
            PendingApply = true,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });
        var task = new DeviceTask
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            LegacyTaskId = 1,
            ModuleName = KeyboardSettingsService.KeyboardModuleName,
            FunctionName = KeyboardSettingsService.InstantApplyFunctionName,
            Status = DeviceTaskStatus.Pending,
            CreatedUtc = DateTime.UtcNow
        };
        context.DeviceTasks.Add(task);
        context.DeviceSettingsApplyLogs.Add(new DeviceSettingsApplyLog
        {
            DeviceId = device.Id,
            SettingsKind = SettingsKind.Keyboard,
            SettingsVersion = settingsVersion,
            ApplyMode = "instant",
            Status = SettingsApplyStatus.Pending,
            TaskId = task.Id,
            LegacyTaskId = task.LegacyTaskId,
            CreatedUtc = DateTime.UtcNow.AddMinutes(-1)
        });
        context.SaveChanges();
        device.KeyboardSettings = context.DeviceKeyboardSettings.Local
            .First(k => k.DeviceId == device.Id);
        return (device, task);
    }

    private static Device SeedDevice(IntellinodeDbContext context, string macAddress)
    {
        if (!context.Tenants.Any(t => t.Id == TenantDefaults.DefaultTenantId))
        {
            context.Tenants.Add(new Tenant { Id = TenantDefaults.DefaultTenantId, Name = "Default" });
        }

        var device = new Device
        {
            Id = Guid.NewGuid(),
            TenantId = TenantDefaults.DefaultTenantId,
            MacAddress = macAddress,
            HostName = "ack-test",
            EnrollmentState = EnrollmentState.Active,
            ClientStatus = "ON"
        };
        context.Devices.Add(device);
        context.SaveChanges();
        return device;
    }
}
