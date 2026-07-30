using FluentAssertions;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Entities.Evaluation;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Enums.Evaluation;
using MPS.Infrastructure.Services;
using ContractEntity = MPS.Domain.Entities.Contract;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// #60 — readiness phải hỏi "HĐ có hiệu lực TRONG KỲ không", không phải "HĐ còn Active BÂY GIỜ không".
/// Người chấm dứt/hết hạn HĐ giữa tháng vẫn phải chốt được lương những ngày đã làm; chỉ HĐ còn Draft
/// (chưa ai ký) mới tính là thiếu dữ liệu.
/// </summary>
public sealed class PayrollReadinessCheckerTests
{
    private const int Year = 2026;
    private const int Month = 6;

    [Theory]
    [InlineData(ContractStatus.Active)]
    [InlineData(ContractStatus.Terminated)]
    [InlineData(ContractStatus.Expired)]
    public async Task Signed_Contract_Covering_Period_Is_Ready_Regardless_Of_Current_Status(ContractStatus status)
    {
        using var fx = new ContractIntegrationFixture();
        var readiness = await SeedAndCheckAsync(fx, status,
            effectiveFrom: new DateTime(Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            effectiveTo: new DateTime(Year, 6, 20, 0, 0, 0, DateTimeKind.Utc));

        readiness.Missing.Should().NotContain(m => m.Contains("hợp đồng"),
            "HĐ đã ký phủ kỳ ⇒ không thiếu dữ liệu HĐ dù nay đã {0}", status);
        readiness.IsReady.Should().BeTrue();
    }

    [Fact]
    public async Task Draft_Only_Contract_Is_Still_Flagged_As_Missing()
    {
        using var fx = new ContractIntegrationFixture();
        var readiness = await SeedAndCheckAsync(fx, ContractStatus.Draft,
            effectiveFrom: new DateTime(Year, 6, 26, 0, 0, 0, DateTimeKind.Utc),
            effectiveTo: new DateTime(Year + 1, 6, 26, 0, 0, 0, DateTimeKind.Utc));

        readiness.Missing.Should().Contain(m => m.Contains("hợp đồng"),
            "HĐ còn Draft (chưa ký) vẫn là thiếu dữ liệu");
    }

    // ── HOL-WARN01 — cảnh báo ngày lễ (không chặn, không ảnh hưởng IsReady) ──

    [Fact]
    public async Task Warns_When_Statutory_Holiday_Month_Has_No_Entered_Holiday()
    {
        using var fx = new ContractIntegrationFixture();
        // Tháng 9 có Quốc khánh 02/09 theo luật; không nhập PublicHolidays ⇒ phải cảnh báo.
        var readiness = await SeedAndCheckAsync(fx, ContractStatus.Active,
            effectiveFrom: new DateTime(Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            effectiveTo: null, year: Year, month: 9);

        readiness.Warnings.Should().Contain(w => w.Contains("ngày lễ"),
            "tháng có lễ luật mà bảng PublicHolidays trống ⇒ cảnh báo");
        readiness.IsReady.Should().BeTrue("cảnh báo KHÔNG được chặn readiness");
    }

    [Fact]
    public async Task No_Warning_When_Statutory_Holiday_Is_Entered()
    {
        using var fx = new ContractIntegrationFixture();
        var readiness = await SeedAndCheckAsync(fx, ContractStatus.Active,
            effectiveFrom: new DateTime(Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            effectiveTo: null, year: Year, month: 9,
            enteredHoliday: (new DateOnly(Year, 9, 2), new DateOnly(Year, 9, 2)));

        readiness.Warnings.Should().BeEmpty("đã nhập đủ ngày lễ ⇒ không cảnh báo");
    }

    /// <summary>
    /// Seed một NV "đủ mọi thứ trừ HĐ" (chấm công + đánh giá Approved + hồ sơ chi lương đủ),
    /// gắn 1 HĐ với trạng thái/khoảng hiệu lực tùy biến, rồi trả readiness của chính NV đó.
    /// </summary>
    private static async Task<PayrollEmployeeReadiness> SeedAndCheckAsync(
        ContractIntegrationFixture fx, ContractStatus contractStatus, DateTime effectiveFrom, DateTime? effectiveTo,
        int year = Year, int month = Month, (DateOnly Start, DateOnly End)? enteredHoliday = null)
    {
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        employeeInfo.BankNumber = "0123456789";
        employeeInfo.TaxNo = "8000000001";

        if (enteredHoliday is { } h)
        {
            await fx.Db.PublicHolidays.AddAsync(new PublicHoliday
            {
                Name = "Lễ",
                HolidayDate = h.Start,
                HolidayEndDate = h.End,
                IsActive = true,
                CreatedAt = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedByUserId = actor.Id,
            });
        }

        var contract = new ContractEntity
        {
            ContractNo = "HDLD-READINESS-1",
            ContractCategory = ContractCategory.Definite,
            ContractSubtype = ContractSubtype.Official,
            Form = ContractForm.Written,
            EmployeeInformationId = employeeInfo.Id,
            EmployerPartyId = party.Id,
            EmployerSignerUserId = actor.Id,
            PositionId = position.Id,
            JobDescription = "x",
            WorkLocation = "HN",
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            SalaryGradeId = grade.Id,
            SalaryAmountSnapshot = 25_000_000m,
            SalaryCurrencySnapshot = LaborLawConstants.Currency.Default,
            SalaryFormType = SalaryFormType.Time,
            SalaryPayCycle = SalaryPayCycle.Monthly,
            WorkingTimeJson = "{}",
            Status = contractStatus,
            CreatedAt = effectiveFrom,
            CreatedByUserId = actor.Id,
        };
        await fx.Db.Contracts.AddAsync(contract);

        await fx.Db.AttendanceRecords.AddAsync(new AttendanceRecord
        {
            EmployeeInformationId = employeeInfo.Id,
            WorkDate = new DateOnly(year, month, 10),
            DayHours = 8m,
            NightHours = 0m,
            Status = AttendanceStatus.Present,
            CreatedAt = new DateTime(year, month, 10, 0, 0, 0, DateTimeKind.Utc),
            CreatedByUserId = actor.Id,
        });

        await fx.Db.MonthlyEvaluations.AddAsync(new MonthlyEvaluation
        {
            EmployeeUserId = employeeInfo.UserId!.Value,
            PeriodYear = year,
            PeriodMonth = month,
            SubjectRoleIdSnapshot = 7,
            Source = EvaluationSource.Direct,
            Status = EvaluationStatus.Approved,
            FinalScore = 90m,
            CreatedByUserId = actor.Id,
            CreatedAt = new DateTime(year, month, 28, 0, 0, 0, DateTimeKind.Utc),
        });

        await fx.Db.SaveChangesAsync();

        var checker = new PayrollReadinessChecker(fx.Db);
        var result = await checker.CheckAsync(new PayrollReadinessRequest(year, month), CancellationToken.None);
        return result.Single();
    }
}
