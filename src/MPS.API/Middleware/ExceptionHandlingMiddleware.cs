using System.Net;
using System.Text.Json;
using FluentValidation;
using MPS.Application.Common.Exceptions.Storage;
using MPS.Domain.Exceptions;

namespace MPS.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException vex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            var errors = vex.Errors.Select(e => e.ErrorMessage).ToList();
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { errors }));
        }
        catch (DomainValidationException dvex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { errors = new[] { dvex.Message } }));
        }
        catch (ForbiddenAccessException fex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = fex.Message }));
        }
        catch (SystemRoleModificationException srex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = srex.Message }));
        }
        catch (RoleInUseException riex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = riex.Message }));
        }
        catch (InvalidObjectKeyException iokex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { errors = new[] { iokex.Message } }));
        }
        catch (FileNotFoundInStorageException fnfex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = fnfex.Message }));
        }
        catch (FileStorageUnavailableException fsuex)
        {
            _logger.LogWarning(fsuex,
                "File storage unavailable for {Method} {Path}",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "Storage is temporarily unavailable." }));
        }
        catch (FileStorageException fsex)
        {
            _logger.LogError(fsex,
                "File storage error for {Method} {Path}",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "A storage error occurred." }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "An unexpected error occurred." }));
        }
    }
}
