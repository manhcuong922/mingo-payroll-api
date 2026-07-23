using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Contract;

namespace MPS.Infrastructure.Persistence;

/// <summary>
/// Cấp QUỸ PHÉP NĂM cho mọi nhân viên đang có hợp đồng hiệu lực.
///
/// <para><b>Vì sao seed phần này.</b> `CAC-CASE-XAY-RA.md` §6: *"Đầu năm mới (01/01) hệ thống tự
/// động cấp quỹ phép cho mọi nhân viên còn hợp đồng hiệu lực"*. Một hệ thống thật chạy tới hôm nay
/// thì job đó **đã chạy rồi**, nên dữ liệu mẫu phải phản ánh trạng thái đó.</para>
///
/// <para><b>Trước khi có bước này</b>, DB dựng mới gần như KHÔNG có bản ghi quỹ phép nào. Hậu quả
/// dây chuyền: duyệt đơn đi muộn/về sớm quá hạn mức bị chặn cứng bằng
/// <c>LEAVE_BALANCE_MISSING</c> (lỗi #34), và mọi luồng nghỉ phép đều phải tự khởi tạo quỹ trước
/// khi làm được việc gì — tức hệ thống mới dựng lên là đã ở trạng thái không dùng được.</para>
///
/// <para>Quy tắc cấp giữ ĐÚNG như <c>InitLeaveBalanceCommandHandler</c> để hai đường không lệch
/// nhau: phép năm = 12 ngày + 1 ngày mỗi 5 năm thâm niên (Đ.113); loại khác lấy hạn mức năm khai
/// trên chính loại nghỉ đó.</para>
///
/// <para>Idempotent theo khoá (nhân viên, năm, loại nghỉ) — chạy lại không nhân đôi, và KHÔNG đụng
/// tới <c>UsedDays</c> của bản ghi đã có.</para>
/// </summary>
public sealed partial class DatabaseSeeder
{
    /// <summary>Đ.113 — số ngày phép năm cơ bản.</summary>
    private const decimal BaseAnnualLeaveDays = 12m;

    /// <summary>Đ.114 — cứ đủ 5 năm thâm niên được thêm 1 ngày.</summary>
    private const int SeniorityBonusEveryYears = 5;

    private const string AnnualLeaveCode = "ANNUAL";

    private async Task SeedLeaveBalancesAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.AddHours(7).Year;

        // Chỉ các loại nghỉ CÓ theo dõi quỹ năm (khớp LeaveTypeAssignmentPolicy.TracksYearlyBalance).
        var leaveTypes = await _context.LeaveTypes
            .AsNoTracking()
            .Where(t => t.IsActive && (t.DeductsBalance || t.MaxDaysPerYear != null))
            .ToListAsync(ct);
        if (leaveTypes.Count == 0)
        {
            _logger.LogDebug("Chưa có loại nghỉ theo dõi quỹ năm; bỏ qua seed quỹ phép.");
            return;
        }

        // Nhân viên có HĐ chưa huỷ → suy ra ngày vào làm (mốc sớm nhất) để tính thâm niên.
        var joinDateByEmployee = await _context.Contracts
            .AsNoTracking()
            .Where(c => c.Status != ContractStatus.Cancelled)
            .GroupBy(c => c.EmployeeInformationId)
            .Select(g => new { EmployeeInformationId = g.Key, JoinDate = g.Min(c => c.EffectiveFrom) })
            .ToDictionaryAsync(x => x.EmployeeInformationId, x => x.JoinDate, ct);
        if (joinDateByEmployee.Count == 0)
        {
            _logger.LogDebug("Chưa có hợp đồng nào; bỏ qua seed quỹ phép.");
            return;
        }

        var existingKeys = (await _context.LeaveBalances
                .AsNoTracking()
                .Where(b => b.Year == year)
                .Select(b => new { b.EmployeeInformationId, b.LeaveTypeId })
                .ToListAsync(ct))
            .Select(x => (x.EmployeeInformationId, x.LeaveTypeId))
            .ToHashSet();

        var adminUserId = await _context.Users.AsNoTracking()
            .Where(u => u.Username == "admin").Select(u => u.Id).FirstOrDefaultAsync(ct);
        if (adminUserId == 0)
        {
            _logger.LogWarning("Không tìm thấy tài khoản admin; bỏ qua seed quỹ phép.");
            return;
        }

        var anchor = new DateTime(year, 1, 1);
        var now = DateTime.UtcNow;
        var added = 0;

        foreach (var (employeeId, joinDate) in joinDateByEmployee)
        {
            foreach (var leaveType in leaveTypes)
            {
                if (existingKeys.Contains((employeeId, leaveType.Id))) continue;

                decimal baseDays, bonusDays;
                if (string.Equals(leaveType.Code, AnnualLeaveCode, StringComparison.OrdinalIgnoreCase))
                {
                    var yearsOfService = anchor < joinDate ? 0 : (int)((anchor - joinDate).TotalDays / 365);
                    baseDays = BaseAnnualLeaveDays;
                    bonusDays = yearsOfService / SeniorityBonusEveryYears;
                }
                else
                {
                    baseDays = leaveType.MaxDaysPerYear ?? leaveType.MaxDaysPerRequest ?? 0m;
                    bonusDays = 0m;
                }

                if (baseDays + bonusDays <= 0m) continue;

                _context.LeaveBalances.Add(new LeaveBalance
                {
                    EmployeeInformationId = employeeId,
                    Year = year,
                    LeaveTypeId = leaveType.Id,
                    BaseDays = baseDays,
                    SeniorityBonusDays = bonusDays,
                    TotalDays = baseDays + bonusDays,
                    UsedDays = 0m,
                    CreatedAt = now,
                    CreatedByUserId = adminUserId,
                });
                added++;
            }
        }

        if (added == 0)
        {
            _logger.LogDebug("Quỹ phép năm {Year} đã đủ; không cần seed thêm.", year);
            return;
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Seeded {Count} bản ghi quỹ phép năm {Year} cho {Employees} nhân viên.",
            added, year, joinDateByEmployee.Count);
    }
}
