using Intellinode.Application.Contracts.Admin;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Options;
using Intellinode.Infrastructure.Persistence;
using Intellinode.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Tests.Keyboard;

public sealed class KeyboardApplyHistoryTests
{
    [Fact]
    public async Task GetApplyHistory_ReturnsKeyboardTasksAndLogs()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedDevice(context, "AA:BB:CC:DD:EE:70:XP");
        var keyboardTaskId = Guid.NewGuid();
        context.DeviceTasks.AddRange(
            new DeviceTask
            {
                DeviceId = device.Id,
                LegacyTaskId = 1,
                ModuleName = "SetRemoteSettings",
                FunctionName = "InstantApply",
                Status = DeviceTaskStatus.Completed,
                CreatedUtc = DateTime.UtcNow.AddMinutes(-30)
            },
            new DeviceTask
            {
                Id = keyboardTaskId,
                DeviceId = device.Id,
                LegacyTaskId = 2,
                ModuleName = KeyboardSettingsService.KeyboardModuleName,
                FunctionName = KeyboardSettingsService.InstantApplyFunctionName,
                Status = DeviceTaskStatus.Failed,
                CreatedUtc = DateTime.UtcNow.AddMinutes(-5)
            });
        context.DeviceSettingsApplyLogs.AddRange(
            new DeviceSettingsApplyLog
            {
                DeviceId = device.Id,
                SettingsKind = SettingsKind.General,
                SettingsVersion = 1,
                ApplyMode = "instant",
                Status = SettingsApplyStatus.Applied,
                CreatedUtc = DateTime.UtcNow.AddMinutes(-20)
            },
            new DeviceSettingsApplyLog
            {
                DeviceId = device.Id,
                SettingsKind = SettingsKind.Keyboard,
                SettingsVersion = 1,
                ApplyMode = "instant",
                Status = SettingsApplyStatus.Pending,
                TaskId = keyboardTaskId,
                LegacyTaskId = 2,
                CreatedUtc = DateTime.UtcNow.AddMinutes(-10)
            });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.GetApplyHistoryAsync(
            device.MacAddress,
            new KeyboardHistoryQuery { Page = 1, PageSize = 20 });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Response!.Data.Items.Count);
        Assert.All(result.Response.Data.Items, i =>
            Assert.True(
                i.ModuleName == KeyboardSettingsService.KeyboardModuleName || i.ApplyMode == "instant",
                "Only keyboard module tasks or keyboard apply logs expected."));
        Assert.DoesNotContain(result.Response.Data.Items, i => i.ModuleName == "SetRemoteSettings");
    }

    [Fact]
    public async Task GetApplyHistory_FiltersByStatusFailed()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedDevice(context, "AA:BB:CC:DD:EE:71:XP");
        context.DeviceTasks.Add(new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = 1,
            ModuleName = KeyboardSettingsService.KeyboardModuleName,
            FunctionName = KeyboardSettingsService.InstantApplyFunctionName,
            Status = DeviceTaskStatus.Failed,
            CreatedUtc = DateTime.UtcNow.AddMinutes(-2)
        });
        context.DeviceSettingsApplyLogs.AddRange(
            new DeviceSettingsApplyLog
            {
                DeviceId = device.Id,
                SettingsKind = SettingsKind.Keyboard,
                SettingsVersion = 1,
                ApplyMode = "instant",
                Status = SettingsApplyStatus.Pending,
                CreatedUtc = DateTime.UtcNow.AddMinutes(-5)
            },
            new DeviceSettingsApplyLog
            {
                DeviceId = device.Id,
                SettingsKind = SettingsKind.Keyboard,
                SettingsVersion = 1,
                ApplyMode = "instant",
                Status = SettingsApplyStatus.Failed,
                Message = "Failed to set keyboard layout: 0x80070057",
                CreatedUtc = DateTime.UtcNow.AddMinutes(-1)
            });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.GetApplyHistoryAsync(
            device.MacAddress,
            new KeyboardHistoryQuery { Status = "Failed", Page = 1, PageSize = 20 });

        Assert.True(result.IsSuccess);
        Assert.All(result.Response!.Data.Items, i => Assert.Equal("Failed", i.ApplyStatus));
        Assert.DoesNotContain(result.Response.Data.Items, i => i.ApplyStatus == "Pending");
    }

    [Fact]
    public async Task GetApplyHistory_Paginates()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedDevice(context, "AA:BB:CC:DD:EE:72:XP");
        for (var i = 0; i < 25; i++)
        {
            context.DeviceSettingsApplyLogs.Add(new DeviceSettingsApplyLog
            {
                DeviceId = device.Id,
                SettingsKind = SettingsKind.Keyboard,
                SettingsVersion = 1,
                ApplyMode = "instant",
                Status = SettingsApplyStatus.Pending,
                CreatedUtc = DateTime.UtcNow.AddMinutes(-i)
            });
        }

        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.GetApplyHistoryAsync(
            device.MacAddress,
            new KeyboardHistoryQuery { Page = 1, PageSize = 10 });

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Response!.Data.Items.Count);
        Assert.Equal(25, result.Response.Data.Pagination.TotalCount);
        Assert.Equal(1, result.Response.Data.Pagination.Page);
        Assert.Equal(10, result.Response.Data.Pagination.PageSize);
    }

    [Fact]
    public async Task GetApplyHistory_DeviceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        SeedTenant(context);
        var service = CreateService(context);

        var result = await service.GetApplyHistoryAsync(
            "AA:BB:CC:DD:EE:99:XP",
            new KeyboardHistoryQuery());

        Assert.False(result.IsSuccess);
        Assert.Equal("DeviceNotFound", result.ErrorCode);
    }

    [Fact]
    public async Task GetApplyHistory_IncludesReasonMessage()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedDevice(context, "AA:BB:CC:DD:EE:73:XP");
        const string reason = "Failed to set keyboard layout: 0x80070057";
        context.DeviceSettingsApplyLogs.Add(new DeviceSettingsApplyLog
        {
            DeviceId = device.Id,
            SettingsKind = SettingsKind.Keyboard,
            SettingsVersion = 1,
            ApplyMode = "instant",
            Status = SettingsApplyStatus.Failed,
            Message = reason,
            CreatedUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.GetApplyHistoryAsync(device.MacAddress, new KeyboardHistoryQuery());

        Assert.True(result.IsSuccess);
        var item = result.Response!.Data.Items.Single(i => i.Message == reason);
        Assert.Equal("Failed", item.ApplyStatus);
        Assert.Equal(reason, item.Message);
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
            Microsoft.Extensions.Options.Options.Create(new KeyboardOptions()));
    }

    private static void SeedTenant(IntellinodeDbContext context)
    {
        if (!context.Tenants.Any(t => t.Id == TenantDefaults.DefaultTenantId))
        {
            context.Tenants.Add(new Tenant { Id = TenantDefaults.DefaultTenantId, Name = "Default" });
            context.SaveChanges();
        }
    }

    private static Device SeedDevice(IntellinodeDbContext context, string macAddress)
    {
        SeedTenant(context);
        var device = new Device
        {
            Id = Guid.NewGuid(),
            TenantId = TenantDefaults.DefaultTenantId,
            MacAddress = macAddress,
            HostName = "history-test",
            EnrollmentState = EnrollmentState.Active,
            ClientStatus = "ON"
        };
        context.Devices.Add(device);
        context.SaveChanges();
        return device;
    }
}
