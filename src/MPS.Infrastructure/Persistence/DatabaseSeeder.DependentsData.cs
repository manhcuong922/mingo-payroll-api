using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Employee;
using MPS.Domain.Enums.Employee;

namespace MPS.Infrastructure.Persistence;

/// <summary>
/// Người phụ thuộc đã đăng ký giảm trừ gia cảnh — sinh từ danh bạ <c>StaffRoster</c>.
///
/// <para><b>Vì sao bắt buộc phải có.</b> <c>PayrollDataCollector</c> đếm SỐ SUẤT giảm trừ từ bản ghi
/// <c>EmployeeDependent</c> còn hiệu lực trong kỳ và <b>cố ý không đọc</b> ô khai tay
/// <c>EmployeeInformation.DependentsNo</c> (#40 — số nhập tay không chứng từ từng tạo ra 99 suất
/// giảm trừ khống). Trước bản này seed chỉ điền ô khai tay ⇒ hồ sơ hiện "2 người phụ thuộc" mà bảng
/// lương giảm trừ <b>0 suất</b>: dữ liệu tự mâu thuẫn, người xem tưởng engine hỏng.</para>
///
/// <para><b>Case cố ý</b> (kế toán trưởng): một suất có <c>RegisteredToDate = 30/06/2026</c> — con
/// đủ 18 tuổi. Kỳ 06/2026 giảm trừ 2 suất, kỳ 07/2026 còn 1 ⇒ cùng mức lương, hai kỳ ra hai số thuế.
/// Đây là nhánh "người phụ thuộc hết hiệu lực giữa năm" mà chỉ dữ liệu mới kiểm được.</para>
///
/// <para>Idempotent theo <c>(EmployeeInformationId, FullName)</c>; không đụng bản ghi HR tự nhập.</para>
/// </summary>
public sealed partial class DatabaseSeeder
{
    private async Task SeedEmployeeDependentsAsync(CancellationToken ct)
    {
        var adminId = await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId).Select(u => u.Id).FirstOrDefaultAsync(ct);
        if (adminId == 0)
        {
            _logger.LogWarning("SeedEmployeeDependents: chưa có admin; bỏ qua.");
            return;
        }

        var usernames = StaffRoster.Where(p => p.Value.Dependents.Count > 0).Select(p => p.Key).ToList();
        if (usernames.Count == 0) return;

        // Hồ sơ nhân sự tra qua tài khoản (danh bạ khoá theo Username).
        var empByUsername = await _context.EmployeeInformations.AsNoTracking()
            .Where(e => e.User != null && usernames.Contains(e.User.Username))
            .Select(e => new { e.User!.Username, e.Id })
            .ToDictionaryAsync(x => x.Username, x => x.Id, StringComparer.OrdinalIgnoreCase, ct);

        var empIds = empByUsername.Values.ToList();
        var existing = (await _context.EmployeeDependents.AsNoTracking()
            .Where(d => empIds.Contains(d.EmployeeInformationId))
            .Select(d => new { d.EmployeeInformationId, d.FullName })
            .ToListAsync(ct))
            .Select(d => (d.EmployeeInformationId, d.FullName))
            .ToHashSet();

        var now = DateTime.UtcNow;
        var toAdd = new List<EmployeeDependent>();
        var seq = 0;

        foreach (var username in usernames)
        {
            if (!empByUsername.TryGetValue(username, out var empId))
            {
                _logger.LogDebug("SeedEmployeeDependents: chưa có hồ sơ cho {Username}; bỏ qua.", username);
                continue;
            }

            foreach (var dep in StaffRoster[username].Dependents)
            {
                seq++;
                if (existing.Contains((empId, dep.FullName))) continue;

                toAdd.Add(new EmployeeDependent
                {
                    EmployeeInformationId = empId,
                    FullName = dep.FullName,
                    DateOfBirth = dep.Dob,
                    Relationship = dep.Relationship,
                    Gender = null,
                    // MST người phụ thuộc: 10 chữ số, sinh xác định để tái lập được và không đụng số thật.
                    TaxIdentificationNumber = $"{8_600_000_000L + seq}",
                    IdentityDocumentType = dep.Relationship == DependentRelationship.Child
                        ? DependentIdentityDocumentType.BirthCertificate
                        : DependentIdentityDocumentType.CitizenId,
                    RegisteredFromDate = dep.RegisteredFrom,
                    RegisteredToDate = dep.RegisteredTo,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedByUserId = adminId,
                });
            }
        }

        if (toAdd.Count == 0)
        {
            _logger.LogDebug("Người phụ thuộc đã đủ; bỏ qua.");
            return;
        }

        await _context.EmployeeDependents.AddRangeAsync(toAdd, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Seeded {Count} người phụ thuộc (giảm trừ gia cảnh) cho {People} nhân sự.",
            toAdd.Count, toAdd.Select(d => d.EmployeeInformationId).Distinct().Count());
    }
}
