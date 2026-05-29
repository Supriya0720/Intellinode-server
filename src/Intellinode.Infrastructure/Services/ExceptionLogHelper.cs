using Intellinode.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services;

public static class ExceptionLogHelper
{
    public static async Task SafeLogAsync(
        IExceptionLogWriter writer,
        ILogger logger,
        string source,
        Exception exception,
        Guid? deviceId = null,
        Guid? adminId = null,
        string? requestPath = null,
        string? httpMethod = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogError(exception, "Unexpected error at {Source}", source);

        try
        {
            await writer.LogAsync(
                source,
                exception,
                deviceId,
                adminId,
                requestPath,
                httpMethod,
                cancellationToken);
        }
        catch (Exception logEx)
        {
            logger.LogError(logEx, "Failed to persist exception log for {Source}", source);
        }
    }
}
