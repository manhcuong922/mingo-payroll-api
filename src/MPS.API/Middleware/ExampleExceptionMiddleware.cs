using System.Net;
using System.Text.Json;

namespace MPS.API.Middleware;

/// <summary>
/// Ví dụ Middleware - xử lý cross-cutting concern ở tầng API (exception handling, logging...).
/// </summary>
public class ExampleExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ExampleExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            var result = JsonSerializer.Serialize(new { error = ex.Message });
            await context.Response.WriteAsync(result);
        }
    }
}
