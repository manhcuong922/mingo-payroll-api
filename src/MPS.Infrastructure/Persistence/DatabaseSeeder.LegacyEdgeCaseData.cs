using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Enums.Employee;
using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Infrastructure.Persistence;

/// <summary>
/// Dữ liệu cho các nhánh NGOẠI LỆ mà đường API bình thường không còn dựng được — nhưng lớp chặn
/// tương ứng vẫn phải hoạt động vì dữ liệu cũ có thể ở trạng thái đó. Không có các bản ghi này thì
/// nhánh chặn là **code không kiểm được**, chỉ tồn tại trên giấy.
///
///  1. Đơn sản xuất Active KHÔNG có tổ trưởng phụ trách (<c>ResponsibleUserId = null</c>).
///     Hôm nay không tạo được qua API (<c>ProjectCreateCommandValidator</c> bắt buộc chọn tổ trưởng,
///     <c>PATCH /project/{id}/responsible</c> nhận <c>int</c> không cho null), nhưng cột vẫn nullable
///     và các đơn sinh trước khi có ràng buộc đó thì đang ở đúng trạng thái này. Guard trong
///     <c>CreateOvertimeRequestCommandHandler</c> ("Đơn sản xuất chưa có tổ trưởng phụ trách") vì thế
///     chỉ đo được trên dữ liệu cũ ⇒ seed 1 đơn đại diện.
///
///  2. Hồ sơ NV ĐÃ KÝ hợp đồng nhưng CHƯA gắn tài khoản (<c>UserId = null</c>) — trạng thái tạm giữa
///     lúc HR nhập hồ sơ + ký HĐ và lúc kích hoạt HĐ sinh tài khoản (xem PRO-01). Người này không thể
///     có bảng đánh giá tháng (đánh giá gắn theo <c>UserId</c>) nên cổng đủ liệu phải liệt kê và chặn
///     cả kỳ — hành vi có chủ đích của <c>PayrollReadinessChecker</c>.
///
/// Idempotent: mỗi mục kiểm tồn tại theo khoá riêng (title / mã HĐ) trước khi chèn.
/// </summary>
public sealed partial class DatabaseSeeder
{
    private const string LeaderlessProjectTitle = "[SX-CU] Đơn tồn kho chưa gán tổ trưởng";
    private const string NoAccountEmployeeCitizenId = "079900112233";
    private const string NoAccountContractNo = "HDLD-CHUA-TK-0001";

    /// <summary>
    /// Số tháng lùi để đặt kỳ hiệu lực HĐ của hồ sơ chưa-gắn-tài-khoản.
    /// Phải nằm NGOÀI <see cref="PayrollDemoMonths"/> kỳ gần nhất (nếu không, hồ sơ này làm readiness
    /// đỏ ngay ở kỳ demo và <see cref="SeedPayrollDemoPeriodsAsync"/> không sinh được kỳ nào), nhưng
    /// vẫn trong 12 tháng gần đây để nhánh readiness đo được.
    /// </summary>
    private const int NoAccountEmployeeMonthsBack = 9;

    private async Task SeedLegacyEdgeCaseDataAsync(CancellationToken ct)
    {
        await SeedLeaderlessProjectAsync(ct);
        await SeedEmployeeWithoutAccountAsync(ct);
    }

    // ===== 1. Đơn sản xuất Active thiếu tổ trưởng (dữ liệu trước khi có ràng buộc bắt buộc) =====
    private async Task SeedLeaderlessProjectAsync(CancellationToken ct)
    {
        if (await _context.Projects.AsNoTracking().AnyAsync(p => p.Title == LeaderlessProjectTitle, ct))
        {
            _logger.LogDebug("Đơn sản xuất thiếu tổ trưởng đã tồn tại; bỏ qua.");
            return;
        }

        var deptId = await _context.Departments.AsNoTracking()
            .Where(d => d.Code == "XSX1").Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
        var adminId = await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId).Select(u => u.Id).FirstOrDefaultAsync(ct);
        if (deptId is null || adminId == 0)
        {
            _logger.LogWarning("SeedLeaderlessProject: thiếu xưởng XSX1 hoặc admin; bỏ qua.");
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        await _context.Projects.AddAsync(new Project
        {
            Title = LeaderlessProjectTitle,
            Description = "Đơn sản xuất tồn từ trước khi hệ thống bắt buộc chọn tổ trưởng phụ trách. "
                        + "Giữ nguyên để kiểm lớp chặn phòng dữ liệu cũ (không đăng ký được OT khi đơn chưa có người phụ trách).",
            Status = ProjectStatus.Active,
            StartDate = today.AddDays(-120),
            Deadline = today.AddDays(60).ToDateTime(new TimeOnly(17, 0), DateTimeKind.Utc),
            Quantity = 50,
            DepartmentId = deptId,
            ResponsibleUserId = null,      // ⭐ chính là trạng thái cần dựng
            CreatedByUserId = adminId,
        }, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded 1 đơn sản xuất Active chưa gán tổ trưởng (dữ liệu cũ).");
    }

    // ===== 2. Hồ sơ NV đã ký HĐ nhưng chưa gắn tài khoản =====
    private async Task SeedEmployeeWithoutAccountAsync(CancellationToken ct)
    {
        if (await _context.Contracts.AsNoTracking().AnyAsync(c => c.ContractNo == NoAccountContractNo, ct))
        {
            _logger.LogDebug("Hồ sơ NV chưa gắn tài khoản đã tồn tại; bỏ qua.");
            return;
        }

        var anchor = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)
            .AddMonths(-NoAccountEmployeeMonthsBack);
        // Không đụng vào kỳ lương đã chốt: nếu tháng đó đã có kỳ thì thêm hồ sơ này sẽ làm snapshot
        // và readiness lệch nhau. Bỏ qua thay vì tạo dữ liệu mâu thuẫn.
        if (await _context.PayrollPeriods.AsNoTracking()
                .AnyAsync(p => p.PeriodYear == anchor.Year && p.PeriodMonth == anchor.Month, ct))
        {
            _logger.LogWarning(
                "SeedEmployeeWithoutAccount: kỳ lương {Month:D2}/{Year} đã tồn tại; bỏ qua để không làm lệch kỳ đã chốt.",
                anchor.Month, anchor.Year);
            return;
        }

        var position = await _context.Positions.AsNoTracking()
            .Where(p => p.Code == "THOCAT").Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        var grade = await _context.SalaryGrades.AsNoTracking()
            .Where(g => g.Code == "THOCAT-1").Select(g => new { g.Id, g.BaseSalary }).FirstOrDefaultAsync(ct);
        var partyId = (await GetCompanyContractPartyAsync(ct))?.Id;
        var adminId = await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId).Select(u => u.Id).FirstOrDefaultAsync(ct);
        if (position is null || grade is null || partyId is null || adminId == 0)
        {
            _logger.LogWarning("SeedEmployeeWithoutAccount: thiếu chức danh/bậc lương/pháp nhân/admin; bỏ qua.");
            return;
        }

        var employee = await _context.EmployeeInformations
            .FirstOrDefaultAsync(e => e.CitizenId == NoAccountEmployeeCitizenId, ct);
        if (employee is null)
        {
            employee = new EmployeeInformation
            {
                UserId = null,                 // ⭐ chưa kích hoạt HĐ ⇒ chưa có tài khoản
                FullName = "Nguyễn Thị Vân Khánh",
                Gender = Gender.Female,
                Dob = new DateTime(1999, 4, 8, 0, 0, 0, DateTimeKind.Utc),
                CitizenId = NoAccountEmployeeCitizenId,
                PermanentAddress = "Xã Cao Viên, Thanh Oai, Hà Nội",
                PhoneNo = "0902100017",
                PositionId = position.Value,
                DependentsNo = 0,
                TaxNo = "8901234567",
                BankNumber = "19001234567890",
            };
            await _context.EmployeeInformations.AddAsync(employee, ct);
            await _context.SaveChangesAsync(ct);
        }

        var effectiveFrom = anchor.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var effectiveTo = anchor.AddMonths(1).AddDays(-1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        await _context.Contracts.AddAsync(NewTestContract(
            NoAccountContractNo,
            ContractEnums.ContractCategory.Definite, ContractEnums.ContractStatus.Expired,
            employee.Id, partyId.Value, adminId, position.Value,
            "Thợ cắt — HĐ ngắn hạn đã ký, hồ sơ chưa được kích hoạt thành tài khoản",
            "Xưởng sản xuất Mingo",
            effectiveFrom, effectiveTo, grade.Id, grade.BaseSalary,
            signedAt: effectiveFrom, signedPlace: "Hà Nội", createdAt: effectiveFrom,
            updatedAt: effectiveTo), ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Seeded 1 hồ sơ NV chưa gắn tài khoản + HĐ đã ký kỳ {Month:D2}/{Year} (cổng đủ liệu phải chặn kỳ đó).",
            anchor.Month, anchor.Year);
    }
}
