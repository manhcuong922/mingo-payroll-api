using MPS.Application.Common.Interfaces;

namespace MPS.API.Services;

public sealed class HttpAuditContextProvider : IAuditContextProvider
{
    private const string CorrelationHeader = "X-Correlation-Id";

    private readonly IHttpContextAccessor _accessor;

    public HttpAuditContextProvider(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public string? IpAddress => _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? RequestPath
    {
        get
        {
            var path = _accessor.HttpContext?.Request.Path.Value;
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
    }

    public string? CorrelationId
    {
        get
        {
            var ctx = _accessor.HttpContext;
            if (ctx is null) return null;
            if (ctx.Request.Headers.TryGetValue(CorrelationHeader, out var raw) && !string.IsNullOrWhiteSpace(raw))
                return raw.ToString();
            return ctx.TraceIdentifier;
        }
    }
}
