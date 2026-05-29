using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Intellinode.Api.Http;
using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Infrastructure.Services;

namespace Intellinode.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IExceptionLogWriter exceptionLogWriter)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var source = $"Unhandled.{context.Request.Method}.{context.Request.Path}";
            var (deviceId, adminId) = ResolveContextIds(context);

            await ExceptionLogHelper.SafeLogAsync(
                exceptionLogWriter,
                _logger,
                source,
                ex,
                deviceId,
                adminId,
                context.Request.Path.Value,
                context.Request.Method,
                context.RequestAborted);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(
                    new AgentErrorResponse
                    {
                        Error = ControllerExceptionExtensions.UnexpectedErrorCode,
                        Message = ControllerExceptionExtensions.UnexpectedErrorMessage
                    },
                    JsonOptions),
                context.RequestAborted);
        }
    }

    private static (Guid? DeviceId, Guid? AdminId) ResolveContextIds(HttpContext context)
    {
        var subject = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
                      context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      context.User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(subject) || !Guid.TryParse(subject, out var id))
        {
            return (null, null);
        }

        if (context.User.IsInRole("Agent"))
        {
            return (id, null);
        }

        if (context.User.IsInRole("Admin"))
        {
            return (null, id);
        }

        return (null, null);
    }
}
