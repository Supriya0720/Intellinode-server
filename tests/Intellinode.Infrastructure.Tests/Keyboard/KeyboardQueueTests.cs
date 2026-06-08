using FluentValidation;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Validators;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Options;
using Intellinode.Infrastructure.Persistence;
using Intellinode.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Tests.Keyboard;

public sealed class KeyboardQueueTests
{
    [Fact]
    public async Task Queue_CreatesUpdateTask_AndQueuedApplyLog()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:80:XP");
        var service = CreateService(context);
        var request = CreateQueueRequest(device.MacAddress, "XP");

        var result = await service.QueueAsync(request, Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal("Queue accepted.", result.Response!.Message);
        var task = await context.DeviceTasks.SingleAsync(t => t.DeviceId == device.Id);
        var applyLog = await context.DeviceSettingsApplyLogs.SingleAsync(l => l.DeviceId == device.Id);

        Assert.Equal(KeyboardSettingsService.KeyboardModuleName, task.ModuleName);
        Assert.Equal(KeyboardSettingsService.QueuedFunctionName, task.FunctionName);
        Assert.Equal("queued", applyLog.ApplyMode);
        Assert.Equal(SettingsApplyStatus.Pending, applyLog.Status);
        Assert.Equal(SettingsKind.Keyboard, applyLog.SettingsKind);
        Assert.True(await context.DeviceKeyboardSettings.AnyAsync(s => s.DeviceId == device.Id && s.PendingApply));
    }

    [Fact]
    public async Task Queue_Blocked_WhenPendingKeyboardTaskExists()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:81:XP");
        context.DeviceTasks.Add(new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = 1,
            ModuleName = KeyboardSettingsService.KeyboardModuleName,
            FunctionName = KeyboardSettingsService.QueuedFunctionName,
            Status = DeviceTaskStatus.Pending
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.QueueAsync(CreateQueueRequest(device.MacAddress, "XP"), Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("ApplyBlocked", result.ErrorCode);
    }

    [Fact]
    public async Task Queue_DryRun_DoesNotPersist()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:82:XP");
        var service = CreateService(context);
        var request = CreateQueueRequest(device.MacAddress, "XP");
        request.Options.DryRun = true;

        var result = await service.QueueAsync(request, Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal(Guid.Empty, result.Response!.Data.TaskId);
        Assert.Empty(await context.DeviceTasks.ToListAsync());
    }

    [Fact]
    public async Task ExecuteNowValidator_RejectsQueueScheduleType()
    {
        var validator = new KeyboardExecuteNowRequestValidator();
        var request = CreateExecuteNowRequest("AA:BB:CC:DD:EE:83:XP", "XP");
        request.Execution.ScheduleType = "Queue";

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task QueueValidator_RejectsInstantApplyScheduleType()
    {
        var validator = new KeyboardQueueRequestValidator();
        var request = CreateQueueRequest("AA:BB:CC:DD:EE:84:XP", "XP");
        request.Execution.ScheduleType = "InstantApply";

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
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
            context.Tenants.Add(new Tenant { Id = TenantDefaults.DefaultTenantId, Name = "Default" });
        }

        var device = new Device
        {
            Id = Guid.NewGuid(),
            TenantId = TenantDefaults.DefaultTenantId,
            MacAddress = macAddress,
            HostName = "queue-test",
            EnrollmentState = EnrollmentState.Active,
            ClientStatus = "ON"
        };
        context.Devices.Add(device);
        context.SaveChanges();
        return device;
    }

    private static KeyboardQueueRequest CreateQueueRequest(string macAddress, string osType) =>
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
                ScheduleType = "Queue"
            },
            Options = new KeyboardOptionsRequest
            {
                DryRun = false,
                ReturnLegacySummary = true,
                CorrelationId = Guid.NewGuid()
            }
        };

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
            Options = new KeyboardOptionsRequest { CorrelationId = Guid.NewGuid() }
        };
}
