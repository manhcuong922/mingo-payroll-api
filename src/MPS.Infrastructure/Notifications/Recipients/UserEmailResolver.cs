using Microsoft.EntityFrameworkCore;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Notifications.Recipients;

/// <summary>
/// Tra email từ AppDbContext (read-only). User.Email = email công ty; EmployeeInformation.Email = cá nhân.
/// Với <c>prefersPersonal = true</c> (N3): ưu tiên EmployeeInformation.Email, fallback User.Email.
/// Ngược lại: ưu tiên User.Email, fallback EmployeeInformation.Email.
/// </summary>
public sealed class UserEmailResolver : IUserEmailResolver
{
    private readonly AppDbContext _db;

    public UserEmailResolver(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string?> ResolveDeliveryEmailAsync(
        int userId, bool prefersPersonal, CancellationToken ct = default)
    {
        var row = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                CompanyEmail = u.Email,
                PersonalEmail = u.EmployeeInformation != null ? u.EmployeeInformation.Email : null,
            })
            .FirstOrDefaultAsync(ct);

        if (row is null) return null;

        return Pick(row.CompanyEmail, row.PersonalEmail, prefersPersonal);
    }

    public async Task<IReadOnlyDictionary<int, string>> ResolveDeliveryEmailsAsync(
        IReadOnlyCollection<int> userIds, bool prefersPersonal, CancellationToken ct = default)
    {
        if (userIds.Count == 0)
            return new Dictionary<int, string>();

        var rows = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                CompanyEmail = u.Email,
                PersonalEmail = u.EmployeeInformation != null ? u.EmployeeInformation.Email : null,
            })
            .ToListAsync(ct);

        var map = new Dictionary<int, string>(rows.Count);
        foreach (var r in rows)
        {
            var email = Pick(r.CompanyEmail, r.PersonalEmail, prefersPersonal);
            if (!string.IsNullOrWhiteSpace(email))
                map[r.Id] = email;
        }
        return map;
    }

    private static string? Pick(string? companyEmail, string? personalEmail, bool prefersPersonal)
    {
        var primary = prefersPersonal ? personalEmail : companyEmail;
        var fallback = prefersPersonal ? companyEmail : personalEmail;

        if (!string.IsNullOrWhiteSpace(primary)) return primary;
        if (!string.IsNullOrWhiteSpace(fallback)) return fallback;
        return null;
    }
}
