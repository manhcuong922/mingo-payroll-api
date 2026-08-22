using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Employee;
using AttendanceEnums = MPS.Domain.Enums.Attendance;
using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Infrastructure.Persistence;

/// <summary>
/// Các trường hợp cố ý làm ĐỎ cổng đủ liệu của kỳ lương — đặt ở THÁNG HIỆN TẠI của thế giới demo
/// (ngay sau kỳ lương cuối cùng), nơi chưa có kỳ nào được chốt.
///
/// <para><b>Vì sao phải tách riêng ra tháng đó.</b> <c>CreatePayrollPeriodCommand</c> ném
/// <c>PayrollNotReadyException</c> khi <b>bất kỳ</b> nhân viên nào thiếu dữ liệu — tức cổng chặn CẢ
/// KỲ chứ không loại riêng người đó. Nhét một case đỏ vào ba kỳ demo là không kỳ nào sinh được, và
/// màn bảng lương trắng trơn. Đây chính là lý do case "hồ sơ chưa gắn tài khoản" trong
/// <c>LegacyEdgeCaseData</c> phải đẩy ra tận 9 tháng trước.</para>
///
/// <para><b>Mục tiêu.</b> Bấm "Tạo kỳ lương" cho tháng hiện tại là thấy ngay bảng chặn liệt kê
/// nhiều lý do khác nhau (R1 đơn tăng ca chưa duyệt xong + R4 hợp đồng còn Draft), thay vì đúng một
/// lý do như trước — người dùng hiểu được cổng này kiểm gì.</para>
///
/// <para>Chạy CUỐI <c>SeedAsync</c>, sau khi ba kỳ demo đã sinh xong. Idempotent theo mã hợp đồng /
/// CCCD / (nhân viên, ngày) và tự bỏ qua nếu tháng đó đã có kỳ lương.</para>
/// </summary>
public sealed partial class DatabaseSeeder
{
    /// <summary>
    /// Tiền tố RIÊNG, cố ý khác <c>HDCASE-</c> của nhóm hợp đồng case: các pha seed khác quét theo
    /// tiền tố đó để gắn phụ cấp, mà hợp đồng chặn cổng thì không được nhận gì thêm — dùng chung
    /// tiền tố sẽ khiến lần seed thứ hai gắn phụ cấp cho chúng (seed hết idempotent).
    /// </summary>
    private const string BlockerContractNoPrefix = "HDBLOCK-";

    private const string BlockerDraftContractNo = BlockerContractNoPrefix + "R4-DRAFT";
    private const string BlockerDraftCitizenId = "038800112244";

    private async Task SeedReadinessBlockersAsync(CancellationToken ct)
    {
        var month = DemoCurrentMonth;
        if (await _context.PayrollPeriods.AsNoTracking()
                .AnyAsync(p => p.PeriodYear == month.Year && p.PeriodMonth == month.Month, ct))
        {
            _logger.LogWarning(
                "SeedReadinessBlockers: kỳ {Month:D2}/{Year} đã được chốt; bỏ qua để không làm lệch kỳ đã có.",
                month.Month, month.Year);
            return;
        }

        var adminId = await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId).Select(u => u.Id).FirstOrDefaultAsync(ct);
        var party = await GetCompanyContractPartyAsync(ct);
        if (adminId == 0 || party is null) return;

        await SeedPendingOvertimeBlockerAsync(month, adminId, ct);
        await SeedDraftContractBlockerAsync(month, party.Id, adminId, ct);
    }

    // ===== R1: còn đơn tăng ca chưa duyệt xong =====
    private async Task SeedPendingOvertimeBlockerAsync(DateOnly month, int adminId, CancellationToken ct)
    {
        var empId = await _context.EmployeeInformations.AsNoTracking()
            .Where(e => e.User != null && e.User.Username == "cn_x3_5")
            .Select(e => (int?)e.Id).FirstOrDefaultAsync(ct);
        if (empId is null) return;

        var day = NearestWorkday(month.AddDays(9));
        if (await _context.OvertimeRequests.AsNoTracking()
                .AnyAsync(o => o.EmployeeInformationId == empId && o.WorkDate == day, ct)) return;

        await _context.OvertimeRequests.AddAsync(new OvertimeRequest
        {
            EmployeeInformationId = empId.Value,
            WorkDate = day,
            OtStartTime = new TimeOnly(18, 0),
            OtEndTime = new TimeOnly(21, 0),
            OtHours = 3m,
            OtDayHours = 3m,
            OtNightHours = 0m,
            // Tổ trưởng đã chuyển tiếp, quản đốc CHƯA duyệt cuối ⇒ cổng đủ liệu chặn kỳ này.
            Status = AttendanceEnums.OtRequestStatus.PendingApprover,
            ApproverRoute = AttendanceEnums.OvertimeApproverRoute.DepartmentManager,
            Reason = "Tăng ca hoàn thiện đơn hàng cuối tháng",
            PmReviewedByUserId = adminId,
            PmReviewedAt = Utc(day.AddDays(-1)),
            PmReviewNote = "Đồng ý, chuyển quản đốc duyệt.",
            CreatedAt = Utc(day.AddDays(-2)),
            CreatedByUserId = adminId,
        }, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded 1 đơn tăng ca chờ duyệt ngày {Day:dd/MM/yyyy} (chặn kỳ lương tháng đó).", day);
    }

    // ===== R4: hợp đồng còn Draft — hồ sơ đã nhập nhưng chưa kích hoạt =====
    private async Task SeedDraftContractBlockerAsync(DateOnly month, int partyId, int adminId, CancellationToken ct)
    {
        if (await _context.Contracts.AsNoTracking().AnyAsync(c => c.ContractNo == BlockerDraftContractNo, ct)) return;

        var emp = await EnsureBlockerEmployeeAsync(
            BlockerDraftCitizenId, "Nguyễn Thị Hồng Ánh", Gender.Female, new DateTime(2001, 2, 17, 0, 0, 0, DateTimeKind.Utc),
            "0902100021", "Xã Thanh Cao, Thanh Oai, Hà Nội", "THOCAT", ct);
        if (emp is null) return;

        var grade = await _context.SalaryGrades.AsNoTracking()
            .Where(g => g.Code == "THOCAT-1").Select(g => new { g.Id, g.BaseSalary }).FirstOrDefaultAsync(ct);
        if (grade is null) return;

        await _context.Contracts.AddAsync(NewTestContract(
            BlockerDraftContractNo,
            ContractEnums.ContractCategory.Definite, ContractEnums.ContractStatus.Draft,
            emp.Id, partyId, adminId, emp.PositionId,
            "Thợ cắt — hồ sơ đã nhập, hợp đồng chưa ký nên chưa được trả lương",
            "Xưởng sản xuất Mingo",
            Utc(month), Utc(month.AddMonths(12).AddDays(-1)), grade.Id, grade.BaseSalary,
            createdAt: Utc(month.AddDays(-3))), ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded 1 hợp đồng còn Draft ở tháng {Month:D2}/{Year} (chặn kỳ lương).",
            month.Month, month.Year);
    }

    private async Task<EmployeeInformation?> EnsureBlockerEmployeeAsync(
        string citizenId, string fullName, Gender gender, DateTime dob,
        string phone, string address, string positionCode, CancellationToken ct)
    {
        var existing = await _context.EmployeeInformations.FirstOrDefaultAsync(e => e.CitizenId == citizenId, ct);
        if (existing is not null) return existing;

        var positionId = await _context.Positions.AsNoTracking()
            .Where(p => p.Code == positionCode).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        var departmentId = await _context.Departments.AsNoTracking()
            .Where(d => d.Code == "XSX1").Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
        if (positionId is null) return null;

        var emp = new EmployeeInformation
        {
            UserId = null,
            FullName = fullName,
            Dob = dob,
            Gender = gender,
            CitizenId = citizenId,
            PermanentAddress = address,
            PhoneNo = phone,
            PositionId = positionId.Value,
            DepartmentId = departmentId,
            DependentsNo = 0,
            IsUnionMember = false,
            TaxNo = "8700000001",
            BankNumber = "19003399881122",
        };
        await _context.EmployeeInformations.AddAsync(emp, ct);
        await _context.SaveChangesAsync(ct);
        return emp;
    }
}
