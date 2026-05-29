using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Intellinode.Api.Http;

public static class ControllerExceptionExtensions
{
    public const string UnexpectedErrorCode = "UnexpectedError";
    public const string UnexpectedErrorMessage = "An unexpected error occurred.";

    public static ObjectResult CreateUnexpectedErrorResult() =>
        new(new AgentErrorResponse
        {
            Error = UnexpectedErrorCode,
            Message = UnexpectedErrorMessage
        })
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };

    public static async Task<ObjectResult> HandleUnexpectedExceptionAsync(
        this ControllerBase controller,
        IExceptionLogWriter writer,
        ILogger logger,
        string actionName,
        Exception ex,
        Guid? deviceId = null,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        var source = $"{controller.GetType().Name}.{actionName}";
        await ExceptionLogHelper.SafeLogAsync(
            writer,
            logger,
            source,
            ex,
            deviceId,
            adminId,
            controller.HttpContext.Request.Path.Value,
            controller.HttpContext.Request.Method,
            cancellationToken);

        return CreateUnexpectedErrorResult();
    }
}
