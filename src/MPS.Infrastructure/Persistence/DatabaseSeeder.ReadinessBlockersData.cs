using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Employee;
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
/// lý do cụ thể (R4 hợp đồng còn Draft) chứ không phải một lỗi chung chung — người dùng hiểu được
/// cổng này kiểm gì.</para>
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

        await SeedDraftContractBlockerAsync(month, party.Id, adminId, ct);
    }

    // ===== R1 — ĐÃ GỠ =====
    // Trước đây seed 1 đơn tăng ca `PendingApprover` ngày 10 của tháng hiện tại cho `cn_x3_5`
    // (Cao Văn Đạt) để cổng đủ liệu có thêm một lý do chặn. Gỡ theo quyết định PO 23/08/2026:
    // `cn_x3_5` đã gánh phụ cấp làm đêm + khoản truy lĩnh kỳ 06, thêm đơn treo làm hồ sơ người này
    // rối mà không thêm giá trị test. Cổng đủ liệu vẫn được kiểm qua R4 (hợp đồng còn Draft) —
    // bảng chặn còn 1 lý do thay vì 2.

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
