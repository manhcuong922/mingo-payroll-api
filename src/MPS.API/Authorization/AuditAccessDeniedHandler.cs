using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using MPS.Application.Common.Interfaces;

namespace MPS.API.Authorization;

/// <summary>
/// Wrapper quanh <see cref="AuthorizationMiddlewareResultHandler"/>:
/// nếu policy enforcement fail với user ĐÃ authenticate → ghi 1 row Action=AccessDenied
/// trước khi trả 403. User chưa authenticate (401) không log để tránh nhiễu log từ
/// các request thăm dò.
/// </summary>
public sealed class AuditAccessDeniedHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();
    private readonly ILogger<AuditAccessDeniedHandler> _logger;

    public AuditAccessDeniedHandler(ILogger<AuditAccessDeniedHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && context.User?.Identity?.IsAuthenticated == true)
        {
            await TryLogAccessDeniedAsync(context);
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    private async Task TryLogAccessDeniedAsync(HttpContext context)
    {
        try
        {
            var auditLogger = context.RequestServices.GetService<IAuditLogger>();
            if (auditLogger is null) return;

            var requestPath = context.Request.Path.Value ?? string.Empty;
            var attemptedAction = $"{context.Request.Method} {requestPath}";

            await auditLogger.LogAccessDeniedAsync(requestPath, attemptedAction, context.RequestAborted);
        }
        catch (Exception ex)
        {
            // Không bao giờ để việc ghi audit cản trở response 403.
            _logger.LogError(ex,
                "Failed to write AccessDenied audit row for {Method} {Path}.",
                context.Request.Method, context.Request.Path);
        }
    }
}
