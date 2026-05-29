using Intellinode.Application.Interfaces;
using Intellinode.Domain.Entities;
using Intellinode.Infrastructure.Persistence;

namespace Intellinode.Infrastructure.Services;

public sealed class AgentCommunicationLogWriter : IAgentCommunicationLogWriter
{
    private readonly IntellinodeDbContext _dbContext;

    public AgentCommunicationLogWriter(IntellinodeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task LogAsync(
        Guid? deviceId,
        string? macAddress,
        string direction,
        string endpoint,
        string? commandCode,
        string? payloadSummary,
        CancellationToken cancellationToken = default)
    {
        _dbContext.AgentCommunicationLogs.Add(new AgentCommunicationLog
        {
            DeviceId = deviceId,
            MacAddress = string.IsNullOrWhiteSpace(macAddress) ? null : macAddress.Trim(),
            Direction = direction,
            Endpoint = endpoint,
            CommandCode = commandCode,
            PayloadSummary = payloadSummary,
            CreatedUtc = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }
}
