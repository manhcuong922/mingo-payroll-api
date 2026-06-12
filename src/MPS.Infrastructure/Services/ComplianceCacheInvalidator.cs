using MediatR;
using Microsoft.Extensions.Caching.Memory;
using MPS.Application.Common.Models.Compliance;

namespace MPS.Infrastructure.Services;

public sealed class ComplianceCacheInvalidator : INotificationHandler<ComplianceVersionChangedNotification>
{
    private readonly IMemoryCache _cache;

    private static readonly Dictionary<string, CancellationTokenSource> _tokenSources = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _lock = new();

    public ComplianceCacheInvalidator(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task Handle(ComplianceVersionChangedNotification notification, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_tokenSources.TryGetValue(notification.ComplianceType, out var oldCts))
            {
                _tokenSources[notification.ComplianceType] = new CancellationTokenSource();
                oldCts.Cancel();
                oldCts.Dispose();
            }
        }
        return Task.CompletedTask;
    }

    public static CancellationToken GetToken(string complianceType)
    {
        lock (_lock)
        {
            if (!_tokenSources.TryGetValue(complianceType, out var cts))
            {
                cts = new CancellationTokenSource();
                _tokenSources[complianceType] = cts;
            }
            return cts.Token;
        }
    }
}
