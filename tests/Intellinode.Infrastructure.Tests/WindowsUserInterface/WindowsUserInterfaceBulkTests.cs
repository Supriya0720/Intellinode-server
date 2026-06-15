using System.Security.Claims;
using Intellinode.Api.Controllers;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Application.Validation;
using Intellinode.Application.Validators;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Options;
using Intellinode.Infrastructure.Persistence;
using Intellinode.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Intellinode.Infrastructure.Tests.WindowsUserInterface;

public sealed class WindowsUserInterfaceBulkTests
{
    [Fact]
    public async Task ExecuteNowBulk_AcceptsAllXpTargets()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var d1 = SeedManagedDevice(context, "AA:BB:CC:DD:EE:11:XP");
        var d2 = SeedManagedDevice(context, "AA:BB:CC:DD:EE:12:XP");
        var service = CreateService(context);
        var request = CreateBulkRequest(d1.MacAddress, d2.MacAddress);

        var result = await service.ExecuteNowBulkAsync(request, Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Response!.Data.Accepted);
        Assert.Equal(0, result.Response.Data.Blocked);
        Assert.Equal(2, await context.DeviceTasks.CountAsync());
    }

    [Fact]
    public async Task ExecuteNowBulk_PartialBlocked_ReturnsPendingTaskExistsReason()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var d1 = SeedManagedDevice(context, "AA:BB:CC:DD:EE:21:XP");
        var d2 = SeedManagedDevice(context, "AA:BB:CC:DD:EE:22:XP");
        context.DeviceTasks.Add(new DeviceTask
        {
            DeviceId = d2.Id,
            LegacyTaskId = 1,
            ModuleName = WindowsUserInterfaceModuleConstants.ModuleName,
            FunctionName = WindowsUserInterfaceModuleConstants.QueuedFunctionName,
            Status = DeviceTaskStatus.Pending
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.ExecuteNowBulkAsync(CreateBulkRequest(d1.MacAddress, d2.MacAddress), Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Response!.Data.Accepted);
        Assert.Equal(1, result.Response.Data.Blocked);
        Assert.Contains(
            result.Response.Data.Results,
            r => r.MacAddress == d2.MacAddress &&
                 r.Reason == WindowsUserInterfaceApplyBlockReason.PendingTaskExists);
    }

    [Fact]
    public async Task ExecuteNowBulk_BlocksInProcessTask_WithInProcessReason()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:31:XP");
        context.DeviceTasks.Add(new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = 1,
            ModuleName = WindowsUserInterfaceModuleConstants.ModuleName,
            FunctionName = WindowsUserInterfaceModuleConstants.InstantFunctionName,
            Status = DeviceTaskStatus.InProcess
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.ExecuteNowAsync(CreateExecuteNowRequest(device.MacAddress), Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("ApplyBlocked", result.ErrorCode);
        Assert.Equal(WindowsUserInterfaceApplyBlockReason.InProcessTaskExists, result.Message);
    }

    [Fact]
    public async Task ExecuteNowBulk_DryRun_QualifiesWithoutPersisting()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:41:XP");
        var service = CreateService(context);
        var request = CreateBulkRequest(device.MacAddress);
        request.Options.DryRun = true;

        var result = await service.ExecuteNowBulkAsync(request, Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Response!.Data.Accepted);
        Assert.Empty(await context.DeviceTasks.ToListAsync());
    }

    [Fact]
    public async Task ExecuteNowBulk_ReturnsLegacySummary_WhenEnabled()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:51:XP");
        var service = CreateService(context, legacySummaryEnabled: true);
        var request = CreateBulkRequest(device.MacAddress);
        request.Options.ReturnLegacySummary = true;

        var result = await service.ExecuteNowBulkAsync(request, Guid.NewGuid());

        Assert.NotNull(result.Response!.Data.LegacySummary);
        Assert.Equal("1", result.Response.Data.LegacySummary!.QualifiedMsg);
        Assert.Equal("...$ApplyGreenSuccess", result.Response.Data.LegacySummary.ErrorMsg);
    }

    [Fact]
    public async Task ExecuteNowGroup_AcceptsActiveGroupDevices()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var groupId = Guid.NewGuid();
        context.DeviceGroups.Add(new DeviceGroup
        {
            Id = groupId,
            TenantId = TenantDefaults.DefaultTenantId,
            Name = "AutologonGroup"
        });
        SeedManagedDevice(context, "AA:BB:CC:DD:EE:61:XP", groupId);
        SeedManagedDevice(context, "AA:BB:CC:DD:EE:62:XP", groupId);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.ExecuteNowGroupAsync(
            groupId,
            CreateGroupRequest(),
            Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Response!.Data.Accepted);
        Assert.Equal(2, await context.DeviceTasks.CountAsync());
    }

    [Fact]
    public async Task ExecuteNowBulkEndpoint_Returns409_WhenPendingTaskExists()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:71:XP");
        context.DeviceTasks.Add(new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = 1,
            ModuleName = WindowsUserInterfaceModuleConstants.ModuleName,
            FunctionName = WindowsUserInterfaceModuleConstants.InstantFunctionName,
            Status = DeviceTaskStatus.Pending
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var actionResult = await controller.ExecuteNow(CreateExecuteNowRequest(device.MacAddress), CancellationToken.None);
        var conflict = Assert.IsType<ConflictObjectResult>(actionResult.Result);
        var error = Assert.IsType<WindowsUserInterfaceErrorResponse>(conflict.Value);

        Assert.Equal("ApplyBlocked", error.Error);
        Assert.Equal("Autologon settings are pending", error.Message);
    }

    [Fact]
    public async Task ExecuteNow_AllowsApply_WhenNoAutologonTasksExist()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:81:XP");
        var service = CreateService(context);

        var result = await service.ExecuteNowAsync(CreateExecuteNowRequest(device.MacAddress), Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Single(await context.DeviceTasks.Where(t => t.DeviceId == device.Id).ToListAsync());
    }

    [Theory]
    [InlineData(WindowsUserInterfaceApplyBlockReason.PendingTaskExists, "Autologon settings are pending")]
    [InlineData(WindowsUserInterfaceApplyBlockReason.InProcessTaskExists, "Autologon settings are in process")]
    [InlineData(WindowsUserInterfaceApplyBlockReason.EnrollmentStateBlocked, "Autologon apply is blocked by enrollment state")]
    public void ApplyBlockReason_FormatsFusionXMessages(string reasonCode, string expectedMessage)
    {
        Assert.Equal(expectedMessage, WindowsUserInterfaceApplyBlockReason.FormatFusionXMessage(reasonCode));
    }

    private static IntellinodeDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<IntellinodeDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new IntellinodeDbContext(options);
    }

    private static WindowsUserInterfaceSettingsService CreateService(
        IntellinodeDbContext context,
        bool legacySummaryEnabled = true)
    {
        var resolver = new EffectiveAgentSettingsResolver(
            context,
            Microsoft.Extensions.Options.Options.Create(new AgentServerOptions
            {
                ServerBaseUrl = "https://localhost:5288",
                ApiBaseUrl = "https://localhost:5288/api/v1",
                DefaultPollIntervalSeconds = 300
            }));

        var dataProtection = new EphemeralDataProtectionProvider();
        return new WindowsUserInterfaceSettingsService(
            context,
            resolver,
            new WindowsUserInterfacePayloadBuilder(),
            new WindowsUserInterfacePasswordProtector(dataProtection),
            Microsoft.Extensions.Options.Options.Create(new WindowsUserInterfaceOptions
            {
                Enabled = true,
                LegacySummaryEnabled = legacySummaryEnabled
            }));
    }

    private static AdminWindowsUserInterfaceController CreateController(IntellinodeDbContext context)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new WindowsUserInterfaceExecuteNowBulkRequestValidator());
        services.AddSingleton(new WindowsUserInterfaceExecuteNowGroupRequestValidator());
        services.AddSingleton(new WindowsUserInterfaceTemplateQueueRequestValidator());

        var serviceProvider = services.BuildServiceProvider();
        var service = CreateService(context);
        var controller = new AdminWindowsUserInterfaceController(
            service,
            new Mock<IExceptionLogWriter>().Object,
            NullLogger<AdminWindowsUserInterfaceController>.Instance,
            new WindowsUserInterfaceExecuteNowRequestValidator(),
            new WindowsUserInterfaceQueueRequestValidator(),
            new WindowsUserInterfaceHistoryQueryValidator(),
            Microsoft.Extensions.Options.Options.Create(new WindowsUserInterfaceOptions { Enabled = true }));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = serviceProvider,
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Role, "Admin")
                ], "TestAuth"))
            }
        };

        return controller;
    }

    private static Device SeedManagedDevice(
        IntellinodeDbContext context,
        string macAddress,
        Guid? groupId = null)
    {
        if (!context.Tenants.Any(t => t.Id == TenantDefaults.DefaultTenantId))
        {
            context.Tenants.Add(new Tenant { Id = TenantDefaults.DefaultTenantId, Name = "Default" });
        }

        var device = new Device
        {
            Id = Guid.NewGuid(),
            TenantId = TenantDefaults.DefaultTenantId,
            GroupId = groupId,
            MacAddress = macAddress,
            HostName = "ui-test",
            EnrollmentState = EnrollmentState.Active,
            ClientStatus = "ON"
        };
        context.Devices.Add(device);
        context.SaveChanges();
        return device;
    }

    private static WindowsUserInterfaceExecuteNowBulkRequest CreateBulkRequest(params string[] macAddresses) =>
        new()
        {
            Targets = macAddresses
                .Select(m => new WindowsUserInterfaceTargetRequest { MacAddress = m, OsType = "XP" })
                .ToList(),
            Settings = new WindowsUserInterfaceSettingsRequest
            {
                UserName = "Administrator",
                AutoLogon = true,
                Password = "P@ssw0rd!"
            },
            Execution = new WindowsUserInterfaceExecutionRequest
            {
                AgentAction = "0",
                ScheduleType = "InstantApply"
            },
            Options = new WindowsUserInterfaceOptionsRequest
            {
                DryRun = false,
                ReturnLegacySummary = false,
                CorrelationId = Guid.NewGuid()
            }
        };

    private static WindowsUserInterfaceExecuteNowGroupRequest CreateGroupRequest() =>
        new()
        {
            Settings = new WindowsUserInterfaceSettingsRequest
            {
                UserName = "Administrator",
                AutoLogon = true,
                Password = "P@ssw0rd!"
            },
            Execution = new WindowsUserInterfaceExecutionRequest
            {
                AgentAction = "0",
                ScheduleType = "InstantApply"
            },
            Options = new WindowsUserInterfaceOptionsRequest
            {
                CorrelationId = Guid.NewGuid()
            }
        };

    private static WindowsUserInterfaceExecuteNowRequest CreateExecuteNowRequest(string macAddress) =>
        new()
        {
            Target = new WindowsUserInterfaceTargetRequest { MacAddress = macAddress, OsType = "XP" },
            Settings = new WindowsUserInterfaceSettingsRequest
            {
                UserName = "Administrator",
                AutoLogon = true,
                Password = "P@ssw0rd!"
            },
            Execution = new WindowsUserInterfaceExecutionRequest
            {
                AgentAction = "0",
                ScheduleType = "InstantApply"
            },
            Options = new WindowsUserInterfaceOptionsRequest
            {
                CorrelationId = Guid.NewGuid()
            }
        };
}
