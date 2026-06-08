using System.Security.Claims;
using Intellinode.Api.Controllers;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Application.Validators;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Options;
using Intellinode.Infrastructure.Persistence;
using Intellinode.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Intellinode.Infrastructure.Tests.SystemSetting;

public sealed class SystemSettingPhase1Tests
{
    [Theory]
    [InlineData("AA:BB:CC:DD:EE:FF:XP", "XP", true)]
    [InlineData("AA:BB:CC:DD:EE:FF:LX", "LX", true)]
    [InlineData("AA:BB:CC:DD:EE:FF:CE", "CE", true)]
    [InlineData("AA:BB:CC:DD:EE:FF:XP", "LX", false)]
    [InlineData("AA:BB:CC:DD:EE:FF", "XP", false)]
    public async Task Validator_EnforcesMacSuffixAndOsMatch(string mac, string osType, bool shouldPass)
    {
        var validator = new SystemSettingExecuteNowRequestValidator();
        var request = CreateExecuteNowRequest(mac, osType);

        var result = await validator.ValidateAsync(request);

        Assert.Equal(shouldPass, result.IsValid);
    }

    [Theory]
    [InlineData("XP", "RemoteSettings")]
    [InlineData("LX", "LxRemoteSettings")]
    [InlineData("CE", "Global_Values")]
    public async Task Service_UsesExpectedOsSpecificSerializerShape(string osType, string expectedToken)
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, macAddress: $"AA:BB:CC:DD:EE:11:{osType}", EnrollmentState.Active);
        var service = CreateService(context);
        var request = CreateExecuteNowRequest(device.MacAddress, osType);

        var result = await service.ExecuteNowAsync(request, Guid.NewGuid());
        var task = await context.DeviceTasks.SingleAsync();

        Assert.True(result.IsSuccess);
        Assert.Contains(expectedToken, task.FunctionParameter, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteNowEndpoint_HappyPath_QueuesTaskAndMarksPendingApply()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:99:XP", EnrollmentState.Active);
        var controller = CreateController(context, featureEnabled: true);
        var request = CreateExecuteNowRequest(device.MacAddress, "XP");

        var actionResult = await controller.ExecuteNow(request, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var payload = Assert.IsType<SystemSettingExecuteNowResponse>(ok.Value);

        var task = await context.DeviceTasks.SingleAsync(t => t.DeviceId == device.Id);
        var settings = await context.DeviceRemoteSettings.SingleAsync(s => s.DeviceId == device.Id);
        var applyLog = await context.DeviceSettingsApplyLogs.SingleAsync(l => l.DeviceId == device.Id);

        Assert.True(payload.Success);
        Assert.Equal("Execute Now queued successfully.", payload.Message);
        Assert.Equal(task.Id, payload.Data.TaskId);
        Assert.True(settings.PendingApply);
        Assert.Equal(SettingsApplyStatus.Pending, applyLog.Status);
    }

    [Fact]
    public async Task ExecuteNowEndpoint_WhenDeviceMissing_ReturnsNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        SeedTenantDefaults(context);
        var controller = CreateController(context, featureEnabled: true);
        var request = CreateExecuteNowRequest("AA:BB:CC:DD:EE:77:XP", "XP");

        var actionResult = await controller.ExecuteNow(request, CancellationToken.None);
        var notFound = Assert.IsType<NotFoundObjectResult>(actionResult.Result);
        var error = Assert.IsType<SystemSettingErrorResponse>(notFound.Value);

        Assert.Equal("DeviceNotFound", error.Error);
    }

    [Fact]
    public async Task ExecuteNowBulkEndpoint_HappyPath_AcceptsAllTargets()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var d1 = SeedManagedDevice(context, "AA:BB:CC:DD:EE:11:XP", EnrollmentState.Active);
        var d2 = SeedManagedDevice(context, "AA:BB:CC:DD:EE:12:XP", EnrollmentState.Active);
        var controller = CreateController(context, featureEnabled: true);
        var request = CreateBulkRequest(
            new[] { d1.MacAddress, d2.MacAddress },
            "XP");

        var actionResult = await controller.ExecuteNowBulk(request, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var payload = Assert.IsType<SystemSettingBulkResponse>(ok.Value);

        Assert.Equal(2, payload.Data.Accepted);
        Assert.Equal(0, payload.Data.Blocked);
        Assert.Equal(2, await context.DeviceTasks.CountAsync());
    }

    [Fact]
    public async Task ExecuteNowBulkEndpoint_PartialBlocked_ReturnsPerTargetResults()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var d1 = SeedManagedDevice(context, "AA:BB:CC:DD:EE:21:XP", EnrollmentState.Active);
        var d2 = SeedManagedDevice(context, "AA:BB:CC:DD:EE:22:XP", EnrollmentState.Active);
        context.DeviceTasks.Add(new DeviceTask
        {
            DeviceId = d2.Id,
            LegacyTaskId = 1,
            ModuleName = "SetRemoteSettings",
            FunctionName = "Apply",
            Status = DeviceTaskStatus.Pending
        });
        context.SaveChanges();

        var controller = CreateController(context, featureEnabled: true);
        var request = CreateBulkRequest(new[] { d1.MacAddress, d2.MacAddress }, "XP");

        var actionResult = await controller.ExecuteNowBulk(request, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var payload = Assert.IsType<SystemSettingBulkResponse>(ok.Value);

        Assert.Equal(1, payload.Data.Accepted);
        Assert.Equal(1, payload.Data.Blocked);
        Assert.Contains(payload.Data.Results, r => r.MacAddress == d2.MacAddress && r.Reason == "PendingTaskExists");
    }

    [Fact]
    public async Task QueueEndpoint_HappyPath_QueuesTask()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:31:XP", EnrollmentState.Active);
        var controller = CreateController(context, featureEnabled: true);
        var request = CreateQueueRequest(device.MacAddress, "XP");

        var actionResult = await controller.Queue(request, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var payload = Assert.IsType<SystemSettingQueueResponse>(ok.Value);

        Assert.Equal("Queue", payload.Data.Execution.ScheduleType);
        Assert.Single(context.DeviceTasks);
    }

    [Fact]
    public async Task TemplateQueueEndpoint_HappyPath_QueuesTaskWithTemplate()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:41:XP", EnrollmentState.Active);
        var controller = CreateController(context, featureEnabled: true);
        var request = CreateTemplateQueueRequest(device.MacAddress, "XP");

        var actionResult = await controller.TemplateQueue(request, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var payload = Assert.IsType<SystemSettingQueueResponse>(ok.Value);

        Assert.Equal("QueueTemplate", payload.Data.Execution.ScheduleType);
        Assert.NotNull(payload.Data.Template);
        Assert.Equal(101, payload.Data.Template!.TemplateId);
    }

    [Fact]
    public async Task FeatureFlagDisabled_ReturnsNotFound_ForCompatEndpoints()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:51:XP", EnrollmentState.Active);
        var controller = CreateController(context, featureEnabled: false);

        var executeNow = await controller.ExecuteNow(CreateExecuteNowRequest(device.MacAddress, "XP"), CancellationToken.None);
        var queue = await controller.Queue(CreateQueueRequest(device.MacAddress, "XP"), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(executeNow.Result);
        Assert.IsType<NotFoundObjectResult>(queue.Result);
    }

    [Fact]
    public async Task BulkValidator_RejectsMixedOsTargets()
    {
        var validator = new SystemSettingExecuteNowBulkRequestValidator();
        var request = CreateBulkRequest(
            new[] { "AA:BB:CC:DD:EE:61:XP", "AA:BB:CC:DD:EE:62:LX" },
            "XP",
            overrideSecondOsType: "LX");

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("same osType", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task QueueValidator_RejectsWrongScheduleType()
    {
        var validator = new SystemSettingQueueRequestValidator();
        var request = CreateQueueRequest("AA:BB:CC:DD:EE:71:XP", "XP");
        request.Execution.ScheduleType = "InstantApply";

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task TemplateQueueValidator_RequiresTemplateValues()
    {
        var validator = new SystemSettingTemplateQueueRequestValidator();
        var request = CreateTemplateQueueRequest("AA:BB:CC:DD:EE:81:XP", "XP");
        request.Execution.TemplateId = 0;
        request.Execution.TemplateName = string.Empty;

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task GetCurrent_HappyPath_ReturnsSettings()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:91:XP", EnrollmentState.Active);
        context.DeviceRemoteSettings.Add(new DeviceRemoteSettings
        {
            DeviceId = device.Id,
            ServerHost = "10.10.20.5",
            ServerPort = 443,
            PollIntervalSeconds = 300,
            CommunicationType = CommunicationType.HTTP,
            AgentEnabled = true,
            DesiredGroupName = "Sales",
            AgentHostName = "sales-gw-01",
            SettingsVersion = 12,
            PendingApply = true,
            LastAppliedVersion = 11,
            LastAppliedUtc = DateTime.UtcNow.AddMinutes(-5),
            InheritFromGroup = false
        });
        context.SaveChanges();
        var controller = CreateController(context, featureEnabled: true);

        var actionResult = await controller.GetCurrent(device.MacAddress, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var payload = Assert.IsType<SystemSettingCurrentResponse>(ok.Value);

        Assert.True(payload.Success);
        Assert.Equal(device.MacAddress, payload.Data.Target.MacAddress);
        Assert.Equal(12, payload.Data.Settings.SettingsVersion);
    }

    [Fact]
    public async Task GetCurrent_DeviceNotFound_ReturnsNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        SeedTenantDefaults(context);
        var controller = CreateController(context, featureEnabled: true);

        var actionResult = await controller.GetCurrent("AA:BB:CC:DD:EE:92:XP", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(actionResult.Result);
    }

    [Fact]
    public async Task GetApplyHistory_HappyPath_WithPagination()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:93:XP", EnrollmentState.Active);
        context.DeviceTasks.AddRange(
            new DeviceTask { DeviceId = device.Id, LegacyTaskId = 1, ModuleName = "SetRemoteSettings", FunctionName = "Queue", Status = DeviceTaskStatus.Pending, CreatedUtc = DateTime.UtcNow.AddMinutes(-20) },
            new DeviceTask { DeviceId = device.Id, LegacyTaskId = 2, ModuleName = "SetRemoteSettings", FunctionName = "InstantApply", Status = DeviceTaskStatus.Completed, CreatedUtc = DateTime.UtcNow.AddMinutes(-10) });
        context.DeviceSettingsApplyLogs.AddRange(
            new DeviceSettingsApplyLog { DeviceId = device.Id, SettingsKind = SettingsKind.General, SettingsVersion = 2, ApplyMode = "queued", Status = SettingsApplyStatus.Pending, CreatedUtc = DateTime.UtcNow.AddMinutes(-15) },
            new DeviceSettingsApplyLog { DeviceId = device.Id, SettingsKind = SettingsKind.General, SettingsVersion = 3, ApplyMode = "instant", Status = SettingsApplyStatus.Applied, CreatedUtc = DateTime.UtcNow.AddMinutes(-5) });
        context.SaveChanges();
        var controller = CreateController(context, featureEnabled: true);

        var actionResult = await controller.GetApplyHistory(device.MacAddress, new SystemSettingHistoryQuery { Page = 1, PageSize = 2 }, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var payload = Assert.IsType<SystemSettingHistoryResponse>(ok.Value);

        Assert.Equal(2, payload.Data.Items.Count);
        Assert.True(payload.Data.Pagination.TotalCount >= 4);
    }

    [Fact]
    public async Task GetApplyHistory_StatusFilter_Works()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:94:XP", EnrollmentState.Active);
        context.DeviceSettingsApplyLogs.AddRange(
            new DeviceSettingsApplyLog { DeviceId = device.Id, SettingsKind = SettingsKind.General, SettingsVersion = 2, ApplyMode = "queued", Status = SettingsApplyStatus.Pending, CreatedUtc = DateTime.UtcNow.AddMinutes(-15) },
            new DeviceSettingsApplyLog { DeviceId = device.Id, SettingsKind = SettingsKind.General, SettingsVersion = 3, ApplyMode = "instant", Status = SettingsApplyStatus.Applied, CreatedUtc = DateTime.UtcNow.AddMinutes(-5) });
        context.SaveChanges();
        var controller = CreateController(context, featureEnabled: true);

        var actionResult = await controller.GetApplyHistory(device.MacAddress, new SystemSettingHistoryQuery { Status = "Applied" }, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var payload = Assert.IsType<SystemSettingHistoryResponse>(ok.Value);

        Assert.All(payload.Data.Items, i => Assert.Equal("Applied", i.ApplyStatus));
    }

    [Fact]
    public async Task GetApplyHistory_InvalidQuery_ReturnsBadRequest()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:95:XP", EnrollmentState.Active);
        var controller = CreateController(context, featureEnabled: true);

        var actionResult = await controller.GetApplyHistory(device.MacAddress, new SystemSettingHistoryQuery { Page = 0 }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(actionResult.Result);
    }

    [Fact]
    public async Task GetEndpoints_FeatureFlagDisabled_ReturnNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var device = SeedManagedDevice(context, "AA:BB:CC:DD:EE:96:XP", EnrollmentState.Active);
        var controller = CreateController(context, featureEnabled: false);

        var settingsResult = await controller.GetCurrent(device.MacAddress, CancellationToken.None);
        var historyResult = await controller.GetApplyHistory(device.MacAddress, new SystemSettingHistoryQuery(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(settingsResult.Result);
        Assert.IsType<NotFoundObjectResult>(historyResult.Result);
    }

    private static IntellinodeDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<IntellinodeDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new IntellinodeDbContext(options);
    }

    private static Device SeedManagedDevice(IntellinodeDbContext context, string macAddress, EnrollmentState enrollmentState)
    {
        SeedTenantDefaults(context);
        var device = new Device
        {
            Id = Guid.NewGuid(),
            TenantId = TenantDefaults.DefaultTenantId,
            GroupId = null,
            HostName = "test-host",
            MacAddress = macAddress,
            EnrollmentState = enrollmentState,
            ClientStatus = "ON",
            CommunicationType = "HTTPS",
            IsOnline = true
        };

        context.Devices.Add(device);
        context.SaveChanges();
        return device;
    }

    private static void SeedTenantDefaults(IntellinodeDbContext context)
    {
        if (!context.Tenants.Any(t => t.Id == TenantDefaults.DefaultTenantId))
        {
            context.Tenants.Add(new Tenant
            {
                Id = TenantDefaults.DefaultTenantId,
                Name = "Default"
            });
        }

        if (!context.TenantAgentDefaults.Any(t => t.TenantId == TenantDefaults.DefaultTenantId))
        {
            context.TenantAgentDefaults.Add(new TenantAgentDefaults
            {
                TenantId = TenantDefaults.DefaultTenantId,
                ServerBaseUrl = "https://localhost:5288",
                ApiBaseUrl = "https://localhost:5288/api/v1",
                DefaultPollIntervalSeconds = 300,
                DefaultCommunicationType = CommunicationType.HTTPS,
                MinPollIntervalHttp = 30,
                UpdatedUtc = DateTime.UtcNow
            });
        }

        context.SaveChanges();
    }

    private static SystemSettingService CreateService(IntellinodeDbContext context)
    {
        var resolver = new EffectiveAgentSettingsResolver(
            context,
            Microsoft.Extensions.Options.Options.Create(new AgentServerOptions
            {
                ServerBaseUrl = "https://localhost:5288",
                ApiBaseUrl = "https://localhost:5288/api/v1",
                DefaultPollIntervalSeconds = 300
            }));

        return new SystemSettingService(context, resolver);
    }

    private static AdminSystemSettingController CreateController(IntellinodeDbContext context, bool featureEnabled)
    {
        var service = CreateService(context);
        var logger = NullLogger<AdminSystemSettingController>.Instance;
        var exceptionLogWriter = new Mock<IExceptionLogWriter>().Object;
        var validator = new SystemSettingExecuteNowRequestValidator();
        var bulkValidator = new SystemSettingExecuteNowBulkRequestValidator();
        var queueValidator = new SystemSettingQueueRequestValidator();
        var templateQueueValidator = new SystemSettingTemplateQueueRequestValidator();
        var historyQueryValidator = new SystemSettingHistoryQueryValidator();
        var controller = new AdminSystemSettingController(
            service,
            exceptionLogWriter,
            logger,
            validator,
            bulkValidator,
            queueValidator,
            templateQueueValidator,
            historyQueryValidator,
            Microsoft.Extensions.Options.Options.Create(new SystemSettingOptions { Enabled = featureEnabled }));

        var adminId = Guid.NewGuid().ToString();
        var user = new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, adminId),
                    new Claim(ClaimTypes.Role, "Admin")
                ],
                "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = user
            }
        };

        return controller;
    }

    private static SystemSettingExecuteNowRequest CreateExecuteNowRequest(string macAddress, string osType) =>
        new()
        {
            Target = new SystemSettingTargetRequest
            {
                MacAddress = macAddress,
                OsType = osType
            },
            Settings = new SystemSettingRemoteSettingsRequest
            {
                ServerIpOrHost = "10.10.20.5",
                PortNo = 443,
                HeartbeatIntervalSeconds = 300,
                CommunicationType = CommunicationType.HTTP,
                ClientStatus = true,
                GroupName = "Sales",
                HostName = "sales-gw-01"
            },
            Execution = new SystemSettingExecutionRequest
            {
                AgentAction = "0",
                ExpiryDurationSeconds = 60,
                ModuleType = "SetRemoteSettings",
                ModuleName = string.Empty,
                Operation = "Update",
                Status = "Pending",
                ScheduleType = "InstantApply"
            },
            Options = new SystemSettingOptionsRequest
            {
                DryRun = false,
                ReturnLegacySummary = true,
                CorrelationId = Guid.NewGuid()
            }
        };

    private static SystemSettingExecuteNowBulkRequest CreateBulkRequest(
        IEnumerable<string> macAddresses,
        string osType,
        string? overrideSecondOsType = null)
    {
        var targets = macAddresses
            .Select((m, i) => new SystemSettingTargetRequest
            {
                MacAddress = m,
                OsType = i == 1 && !string.IsNullOrWhiteSpace(overrideSecondOsType) ? overrideSecondOsType : osType
            })
            .ToList();

        return new SystemSettingExecuteNowBulkRequest
        {
            Targets = targets,
            Settings = CreateExecuteNowRequest(targets[0].MacAddress, osType).Settings,
            Execution = new SystemSettingExecutionRequest
            {
                ScheduleType = "InstantApply",
                ModuleType = "SetRemoteSettings",
                Status = "Pending",
                Operation = "Update"
            },
            Options = new SystemSettingOptionsRequest
            {
                CorrelationId = Guid.NewGuid(),
                ReturnLegacySummary = true
            }
        };
    }

    private static SystemSettingQueueRequest CreateQueueRequest(string macAddress, string osType) =>
        new()
        {
            Target = new SystemSettingTargetRequest { MacAddress = macAddress, OsType = osType },
            Settings = CreateExecuteNowRequest(macAddress, osType).Settings,
            Execution = new SystemSettingExecutionRequest
            {
                ScheduleType = "Queue",
                ModuleType = "SetRemoteSettings",
                Status = "Pending",
                Operation = "Update"
            },
            Options = new SystemSettingOptionsRequest
            {
                CorrelationId = Guid.NewGuid(),
                ReturnLegacySummary = true
            }
        };

    private static SystemSettingTemplateQueueRequest CreateTemplateQueueRequest(string macAddress, string osType) =>
        new()
        {
            Target = new SystemSettingTargetRequest { MacAddress = macAddress, OsType = osType },
            Settings = CreateExecuteNowRequest(macAddress, osType).Settings,
            Execution = new SystemSettingExecutionRequest
            {
                ScheduleType = "QueueTemplate",
                ModuleType = "SetRemoteSettings",
                Status = "Pending",
                Operation = "Update",
                TemplateId = 101,
                TemplateName = "BranchTemplateA"
            },
            Options = new SystemSettingOptionsRequest
            {
                CorrelationId = Guid.NewGuid(),
                ReturnLegacySummary = true
            }
        };
}
