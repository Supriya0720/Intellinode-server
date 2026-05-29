using Intellinode.Application.Interfaces;
using Intellinode.Domain.Entities;
using Intellinode.Infrastructure.Persistence;

namespace Intellinode.Infrastructure.Services;

public sealed class ExceptionLogWriter : IExceptionLogWriter
{
    private const int MaxTextLength = 8000;

    private readonly IntellinodeDbContext _dbContext;

    public ExceptionLogWriter(IntellinodeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LogAsync(
        string source,
        Exception exception,
        Guid? deviceId = null,
        Guid? adminId = null,
        string? requestPath = null,
        string? httpMethod = null,
        CancellationToken cancellationToken = default)
    {
        _dbContext.ExceptionLogs.Add(new ExceptionLog
        {
            Source = TruncateRequired(source, 256),
            Message = TruncateRequired(exception.ToString(), MaxTextLength),
            StackTrace = TruncateOptional(exception.StackTrace, MaxTextLength),
            RequestPath = TruncateOptional(requestPath, 512),
            HttpMethod = TruncateOptional(httpMethod, 16),
            DeviceId = deviceId,
            AdminId = adminId,
            LoggedUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string TruncateRequired(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string? TruncateOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
