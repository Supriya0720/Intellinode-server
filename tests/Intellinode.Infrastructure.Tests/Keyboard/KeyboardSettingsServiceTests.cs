using Intellinode.Application.Contracts.Admin;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Options;
using Intellinode.Infrastructure.Persistence;
using Intellinode.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
namespace Intellinode.Infrastructure.Tests.Keyboard;

public sealed class KeyboardSettingsServiceTests
{
    [Fact]
    public async Task ExecuteNow_UpsertsSettings_AndQueuesKeyboardTask()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:10:XP");
        var service = CreateService(context);
        var request = CreateExecuteNowRequest(device.MacAddress, "XP");

        var result = await service.ExecuteNowAsync(request, Guid.NewGuid());

        Assert.True(result.IsSuccess);
        var keyboard = await context.DeviceKeyboardSettings.SingleAsync(s => s.DeviceId == device.Id);
        var task = await context.DeviceTasks.SingleAsync(t => t.DeviceId == device.Id);
        var applyLog = await context.DeviceSettingsApplyLogs.SingleAsync(l => l.DeviceId == device.Id);

        Assert.Equal(2, keyboard.Delay);
        Assert.Equal(31, keyboard.RepeatRate);
        Assert.Equal("English (United States)", keyboard.KeyboardLocale);
        Assert.True(keyboard.PendingApply);
        Assert.Equal(1, keyboard.SettingsVersion);
        Assert.Equal(KeyboardSettingsService.KeyboardModuleName, task.ModuleName);
        Assert.Equal(KeyboardSettingsService.InstantApplyFunctionName, task.FunctionName);
        Assert.Equal($"{device.MacAddress}&SCR", task.ExtraData);
        Assert.Contains("XPKeyboard", task.FunctionParameter, StringComparison.Ordinal);
        Assert.Equal(SettingsKind.Keyboard, applyLog.SettingsKind);
        Assert.Equal(task.Id, applyLog.TaskId);
        Assert.Equal(task.LegacyTaskId, applyLog.LegacyTaskId);
    }

    [Fact]
    public async Task ExecuteNow_Blocked_WhenPendingKeyboardTaskExists()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:20:XP");
        context.DeviceTasks.Add(new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = 1,
            ModuleName = KeyboardSettingsService.KeyboardModuleName,
            FunctionName = KeyboardSettingsService.InstantApplyFunctionName,
            Status = DeviceTaskStatus.Pending
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.ExecuteNowAsync(CreateExecuteNowRequest(device.MacAddress, "XP"), Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("ApplyBlocked", result.ErrorCode);
        Assert.Equal("PendingTaskExists", result.Message);
    }

    [Fact]
    public async Task ExecuteNow_DryRun_DoesNotPersist()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:30:XP");
        var service = CreateService(context);
        var request = CreateExecuteNowRequest(device.MacAddress, "XP");
        request.Options.DryRun = true;

        var result = await service.ExecuteNowAsync(request, Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal(Guid.Empty, result.Response!.Data.TaskId);
        Assert.Empty(await context.DeviceKeyboardSettings.ToListAsync());
        Assert.Empty(await context.DeviceTasks.ToListAsync());
        Assert.Empty(await context.DeviceSettingsApplyLogs.ToListAsync());
    }

    [Fact]
    public async Task GetCurrent_ReturnsStoredSettings()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:40:XP");
        context.DeviceKeyboardSettings.Add(new DeviceKeyboardSettings
        {
            DeviceId = device.Id,
            Delay = 2,
            RepeatRate = 31,
            KeyboardLocale = "English (United States)",
            ReplaceExistingKeyboard = false,
            SettingsVersion = 5,
            PendingApply = true,
            LastAppliedVersion = 4,
            LastAppliedUtc = DateTime.UtcNow.AddMinutes(-10),
            LastApplyStatus = "Applied",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.GetCurrentAsync(device.MacAddress);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Response!.Data.Settings.Delay);
        Assert.Equal(31, result.Response.Data.Settings.RepeatRate);
        Assert.Equal(5, result.Response.Data.Settings.SettingsVersion);
        Assert.Equal("device", result.Response.Data.Compat.Source);
    }

    [Fact]
    public void BuildLegacyKeyboardPayload_FitsWithin512Chars()
    {
        var payload = KeyboardSettingsService.BuildLegacyKeyboardPayload(
            new KeyboardTargetRequest { MacAddress = "AA:BB:CC:DD:EE:50:XP", OsType = "XP" },
            new KeyboardSettingsRequest
            {
                Delay = 2,
                RepeatRate = 31,
                KeyboardLocale = "English (United States)",
                ReplaceExistingKeyboard = false
            });

        Assert.True(payload.Length <= KeyboardSettingsService.MaxFunctionParameterLength);
        Assert.Contains("iDelay", payload, StringComparison.Ordinal);
        Assert.Contains("iRepeat_Rate", payload, StringComparison.Ordinal);
    }

    private static IntellinodeDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<IntellinodeDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new IntellinodeDbContext(options);
    }

    private static KeyboardSettingsService CreateService(IntellinodeDbContext context)
    {
        var resolver = new EffectiveAgentSettingsResolver(
            context,
            Microsoft.Extensions.Options.Options.Create(new AgentServerOptions
            {
                ServerBaseUrl = "https://localhost:5288",
                ApiBaseUrl = "https://localhost:5288/api/v1",
                DefaultPollIntervalSeconds = 300
            }));

        return new KeyboardSettingsService(
            context,
            resolver,
            Microsoft.Extensions.Options.Options.Create(new KeyboardOptions { DefaultSignalSuffix = "SCR" }));
    }

    private static Device SeedManagedDevice(IntellinodeDbContext context, string macAddress)
    {
        if (!context.Tenants.Any(t => t.Id == TenantDefaults.DefaultTenantId))
        {
            context.Tenants.Add(new Tenant
            {
                Id = TenantDefaults.DefaultTenantId,
                Name = "Default"
            });
        }

        var device = new Device
        {
            Id = Guid.NewGuid(),
            TenantId = TenantDefaults.DefaultTenantId,
            HostName = "keyboard-test",
            MacAddress = macAddress,
            EnrollmentState = EnrollmentState.Active,
            ClientStatus = "ON",
            CommunicationType = "HTTPS",
            IsOnline = true
        };

        context.Devices.Add(device);
        context.SaveChanges();
        return device;
    }

    private static KeyboardExecuteNowRequest CreateExecuteNowRequest(string macAddress, string osType) =>
        new()
        {
            Target = new KeyboardTargetRequest { MacAddress = macAddress, OsType = osType },
            Settings = new KeyboardSettingsRequest
            {
                Delay = 2,
                RepeatRate = 31,
                KeyboardLocale = "English (United States)",
                ReplaceExistingKeyboard = false
            },
            Execution = new KeyboardExecutionRequest
            {
                AgentAction = "0",
                ScheduleType = "InstantApply"
            },
            Options = new KeyboardOptionsRequest
            {
                DryRun = false,
                ReturnLegacySummary = true,
                CorrelationId = Guid.NewGuid()
            }
        };
}
