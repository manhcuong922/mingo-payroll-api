using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Attendance;

namespace MPS.Infrastructure.Persistence;

/// <summary>
/// Ngày nghỉ lễ, tết hưởng nguyên lương theo Điều 112 BLLĐ 2019 (11 ngày/năm).
///
/// <para><b>Vì sao phải seed.</b> Không có bảng này thì: (1) <c>DayTypeResolver</c> không bao giờ
/// trả <c>DayType.PublicHoliday</c> ⇒ nhánh phụ trội 300% ngày lễ của
/// <c>RegularHoursPremiumCalculator</c> và <c>OvertimeMultiplierCalculator</c> là code không kiểm
/// được; (2) công chuẩn tháng tính sai; (3) cảnh báo cấp kỳ <c>HOL-WARN01</c>
/// (<c>PayrollReadinessChecker.BuildPeriodWarningsAsync</c>) bật ở MỌI kỳ nên mất tác dụng báo động.</para>
///
/// <para><b>Phạm vi ngày.</b> Chỉ seed 2025–2026 (khoảng dữ liệu demo thực sự dùng) cộng Tết dương
/// 01/01/2027. Ngày lễ âm lịch của các năm xa hơn phải tra lịch từng năm — seed số phỏng đoán vào một
/// hệ thống tính lương là tự tạo dữ liệu sai.</para>
///
/// <para><b>Số ngày nghỉ Tết = 5 ngày LUẬT ĐỊNH</b> (29 tháng Chạp + mùng 1..4), không phải lịch nghỉ
/// hoán đổi do Chính phủ công bố từng năm (thường 7–9 ngày). Lịch hoán đổi là quyết định hành chính
/// theo năm, gán vào đây sẽ thổi phồng số ngày hưởng 300%.</para>
///
/// <para>Idempotent theo <c>(HolidayDate, Name)</c> — chạy lại không nhân đôi và không đụng ngày lễ
/// do HR tự nhập.</para>
/// </summary>
public sealed partial class DatabaseSeeder
{
    private sealed record HolidaySpec(DateOnly From, DateOnly To, string Name, string Description);

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private static readonly HolidaySpec[] PublicHolidaySeeds =
    {
        // ===== 2025 =====
        new(D(2025, 1, 1),  D(2025, 1, 1),  "Tết Dương lịch", "Điều 112 k.1 điểm a BLLĐ 2019 — 1 ngày."),
        new(D(2025, 1, 28), D(2025, 2, 1),  "Tết Nguyên đán Ất Tỵ",
            "Điều 112 k.1 điểm b — 5 ngày: 29 tháng Chạp (28/01) và mùng 1..4 (mùng 1 = 29/01/2025)."),
        new(D(2025, 4, 7),  D(2025, 4, 7),  "Giỗ Tổ Hùng Vương", "Điều 112 k.1 điểm e — 10/3 âm lịch."),
        new(D(2025, 4, 30), D(2025, 5, 1),  "Ngày Chiến thắng & Quốc tế Lao động",
            "Điều 112 k.1 điểm c + d — 30/4 và 01/5 liền kề."),
        new(D(2025, 9, 1),  D(2025, 9, 2),  "Quốc khánh", "Điều 112 k.1 điểm đ — 02/9 và 1 ngày liền kề."),

        // ===== 2026 =====
        new(D(2026, 1, 1),  D(2026, 1, 1),  "Tết Dương lịch", "Điều 112 k.1 điểm a BLLĐ 2019 — 1 ngày."),
        new(D(2026, 2, 16), D(2026, 2, 20), "Tết Nguyên đán Bính Ngọ",
            "Điều 112 k.1 điểm b — 5 ngày: 29 tháng Chạp (16/02) và mùng 1..4 (mùng 1 = 17/02/2026)."),
        new(D(2026, 4, 26), D(2026, 4, 26), "Giỗ Tổ Hùng Vương", "Điều 112 k.1 điểm e — 10/3 âm lịch."),
        new(D(2026, 4, 30), D(2026, 5, 1),  "Ngày Chiến thắng & Quốc tế Lao động",
            "Điều 112 k.1 điểm c + d — rơi vào kỳ lương 05/2026, là mốc kiểm phụ trội ngày lễ."),
        new(D(2026, 9, 2),  D(2026, 9, 3),  "Quốc khánh", "Điều 112 k.1 điểm đ — 02/9 và 1 ngày liền kề."),

        // ===== 2027 (chỉ ngày dương lịch chắc chắn) =====
        new(D(2027, 1, 1),  D(2027, 1, 1),  "Tết Dương lịch", "Điều 112 k.1 điểm a BLLĐ 2019 — 1 ngày."),
    };

    private async Task SeedPublicHolidaysAsync(CancellationToken ct)
    {
        var adminId = await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId).Select(u => u.Id).FirstOrDefaultAsync(ct);
        if (adminId == 0)
        {
            _logger.LogWarning("SeedPublicHolidays: chưa có admin; bỏ qua.");
            return;
        }

        var existing = (await _context.PublicHolidays.AsNoTracking()
            .Select(h => new { h.HolidayDate, h.Name }).ToListAsync(ct))
            .Select(h => (h.HolidayDate, h.Name))
            .ToHashSet();

        var now = DateTime.UtcNow;
        var toAdd = PublicHolidaySeeds
            .Where(s => !existing.Contains((s.From, s.Name)))
            .Select(s => new PublicHoliday
            {
                Name = s.Name,
                HolidayDate = s.From,
                HolidayEndDate = s.To,
                Description = s.Description,
                IsActive = true,
                CreatedAt = now,
                CreatedByUserId = adminId,
            })
            .ToList();

        if (toAdd.Count == 0)
        {
            _logger.LogDebug("Ngày lễ đã đủ; bỏ qua.");
            return;
        }

        await _context.PublicHolidays.AddRangeAsync(toAdd, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} kỳ nghỉ lễ (Điều 112 BLLĐ 2019).", toAdd.Count);
    }
}
