using FluentAssertions;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Enums.Contract;
using MPS.Infrastructure.Services;
using ContractEntity = MPS.Domain.Entities.Contract;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// Phase 3 — cổng readiness KHÔNG được đòi "bảng đánh giá tháng đã duyệt" ở hợp đồng DÂN SỰ
/// (khoán việc / dịch vụ / cộng tác viên).
///
/// <para><b>Vì sao.</b> Đánh giá tháng là công cụ quản trị nhân sự của quan hệ LAO ĐỘNG — người ký
/// HĐ dân sự không nằm trong quy trình đó nên sẽ KHÔNG BAO GIỜ có bản đánh giá được duyệt. Mà
/// <c>CreatePayrollPeriodCommand</c> chặn CẢ KỲ khi bất kỳ NV nào chưa đủ liệu ⇒ chỉ một hợp đồng
/// khoán là <b>không tạo được bảng lương cho toàn công ty</b>.</para>
///
/// <para>Chấm công thì VẪN đòi: quyết định PO 18/08/2026 dùng bảng chấm công / phân ca bình thường
/// làm căn cứ tính khoản hỗ trợ theo tiến độ của HĐ khoán.</para>
/// </summary>
public sealed class PayrollReadinessCivilContractTests
{
    private const int Year = 2026;
    private const int Month = 6;

    [Theory(DisplayName = "HĐ dân sự không cần đánh giá tháng vẫn đủ điều kiện chốt lương")]
    [InlineData(ContractKind.PieceWork)]
    [InlineData(ContractKind.Service)]
    [InlineData(ContractKind.Collaborator)]
    public async Task CivilContract_DoesNotRequireMonthlyEvaluation(ContractKind kind)
    {
        using var fx = new ContractIntegrationFixture();
        var readiness = await SeedAndCheckAsync(fx, kind, withAttendance: true);

        readiness.Missing.Should().NotContain(m => m.Contains("đánh giá"),
            "người ký HĐ dân sự không thuộc quy trình đánh giá tháng — đòi bản đánh giá là chặn vĩnh viễn");
        readiness.IsReady.Should().BeTrue();
    }

    [Fact(DisplayName = "HĐ dân sự VẪN cần dữ liệu chấm công (căn cứ tính hỗ trợ theo giờ)")]
    public async Task CivilContract_StillRequiresAttendance()
    {
        using var fx = new ContractIntegrationFixture();
        var readiness = await SeedAndCheckAsync(fx, ContractKind.PieceWork, withAttendance: false);

        readiness.Missing.Should().Contain(m => m.Contains("chấm công"),
            "mốc 150h/200h của khoản hỗ trợ lấy từ chấm công — thiếu chấm công là thiếu căn cứ trả tiền");
        readiness.IsReady.Should().BeFalse();
    }

    [Fact(DisplayName = "HĐLĐ vẫn bị chặn khi thiếu đánh giá tháng (không nới lỏng nhầm)")]
    public async Task LaborContract_StillRequiresMonthlyEvaluation()
    {
        using var fx = new ContractIntegrationFixture();
        var readiness = await SeedAndCheckAsync(fx, ContractKind.LaborFullTime, withAttendance: true);

        readiness.Missing.Should().Contain(m => m.Contains("đánh giá"));
        readiness.IsReady.Should().BeFalse();
    }

    /// <summary>
    /// Seed NV có HĐ đã ký phủ kỳ + hồ sơ chi lương đủ, KHÔNG có bản đánh giá tháng nào.
    /// </summary>
    private static async Task<PayrollEmployeeReadiness> SeedAndCheckAsync(
        ContractIntegrationFixture fx, ContractKind kind, bool withAttendance)
    {
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        employeeInfo.BankNumber = "0123456789";
        employeeInfo.TaxNo = "8000000001";

        await fx.Db.Contracts.AddAsync(new ContractEntity
        {
            ContractNo = "KV-READINESS-1",
            ContractCategory = kind == ContractKind.LaborFullTime ? ContractCategory.Definite : null,
            ContractSubtype = ContractSubtype.Official,
            Form = ContractForm.Written,
            EmployeeInformationId = employeeInfo.Id,
            EmployerPartyId = party.Id,
            EmployerSignerUserId = actor.Id,
            PositionId = position.Id,
            JobDescription = "Khoán gia công",
            WorkLocation = "HN",
            Kind = kind,
            EffectiveFrom = new DateTime(Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo = new DateTime(Year, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            SalaryGradeId = grade.Id,
            SalaryAmountSnapshot = 12_000_000m,
            SalaryCurrencySnapshot = LaborLawConstants.Currency.Default,
            SalaryFormType = SalaryFormType.Time,
            SalaryPayCycle = SalaryPayCycle.Monthly,
            WorkingTimeJson = "{}",
            Status = ContractStatus.Active,
            CreatedAt = new DateTime(Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedByUserId = actor.Id,
        });

        if (withAttendance)
        {
            await fx.Db.AttendanceRecords.AddAsync(new AttendanceRecord
            {
                EmployeeInformationId = employeeInfo.Id,
                WorkDate = new DateOnly(Year, Month, 10),
                DayHours = 8m,
                NightHours = 0m,
                Status = AttendanceStatus.Present,
                CreatedAt = new DateTime(Year, Month, 10, 0, 0, 0, DateTimeKind.Utc),
                CreatedByUserId = actor.Id,
            });
        }

        await fx.Db.SaveChangesAsync();

        var checker = new PayrollReadinessChecker(fx.Db);
        var result = await checker.CheckAsync(new PayrollReadinessRequest(Year, Month), CancellationToken.None);
        return result.Single();
    }
}
