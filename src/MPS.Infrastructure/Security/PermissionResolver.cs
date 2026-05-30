using Microsoft.Extensions.Caching.Memory;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Interfaces;

namespace MPS.Infrastructure.Security;

public sealed class PermissionResolver : IPermissionResolver
{
    private const string CacheKeyPrefix = "perms:role:";
    private static readonly TimeSpan AbsoluteTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SlidingTtl = TimeSpan.FromMinutes(2);

    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IMemoryCache _cache;

    public PermissionResolver(IRolePermissionRepository rolePermissionRepository, IMemoryCache cache)
    {
        _rolePermissionRepository = rolePermissionRepository;
        _cache = cache;
    }

    public async Task<IReadOnlyCollection<string>> GetPermissionsForRoleAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(roleId);

        if (_cache.TryGetValue(key, out IReadOnlyCollection<string>? cached) && cached is not null)
            return cached;

        var permissions = await _rolePermissionRepository.GetPermissionCodesByRoleIdAsync(roleId, cancellationToken);
        var snapshot = permissions.ToArray();

        _cache.Set(key, (IReadOnlyCollection<string>)snapshot, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = AbsoluteTtl,
            SlidingExpiration = SlidingTtl,
        });

        return snapshot;
    }

    public void InvalidateRoleCache(int roleId) => _cache.Remove(CacheKey(roleId));

    private static string CacheKey(int roleId) => CacheKeyPrefix + roleId;
}
