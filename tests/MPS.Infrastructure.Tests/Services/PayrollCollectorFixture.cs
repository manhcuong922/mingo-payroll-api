using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Enums.Employee;
using MPS.Infrastructure.Persistence;
using MPS.Infrastructure.Services;
using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// Nền dựng sẵn cho các test của <see cref="PayrollDataCollector"/> — service này trước đây không có
/// test nào, nên toàn bộ suite vẫn xanh trong khi nhiều lỗi trả sai lương tồn tại.
///
/// <para>Kỳ chuẩn dùng chung: <b>07/2026</b> (01/07 là Thứ Tư; các Thứ Bảy: 04, 11, 18, 25),
/// lương 10.000.000đ, công chuẩn 26 ⇒ đơn giá giờ = 10.000.000 / (26 × 8) ≈ 48.076,92đ.
/// Số liệu compliance lấy từ <see cref="FakeComplianceLookup"/> (cố định, không phụ thuộc DB).</para>
/// </summary>
internal static class PayrollCollectorFixture
{
    public const int EmpId = 1;
    public const int UserId = 1;
    public const int PositionId = 1;
    public const int ShiftId = 1;
    public const int ContractId = 1;

    public const int Year = 2026;
    public const int Month = 7;
    public static readonly DateOnly PayDate = new(2026, 8, 5);

    public const decimal Salary = 10_000_000m;
    public const decimal StandardDays = 26m;
    public const decimal HoursPerDay = 8m;

    /// <summary>Đơn giá 1 giờ công theo mức lương và công chuẩn của fixture.</summary>
    public static decimal HourlyRate => Salary / (StandardDays * HoursPerDay);

    public static DateOnly D(int day) => new(Year, Month, day);

    public static AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"payroll-collector-{Guid.NewGuid():N}")
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    /// <summary>
    /// Một NV có hợp đồng chính thức còn hiệu lực suốt kỳ. <paramref name="employeeCode"/> = null
    /// ⇒ NV CHƯA có tài khoản (dùng cho case mã NV rơi về số CCCD).
    /// </summary>
    public static async Task<AppDbContext> SeedBaseAsync(
        bool socialFlag = true, bool healthFlag = true, bool unemploymentFlag = true,
        string? employeeCode = "NV001")
    {
        var db = NewContext();

        // `employeeCode = null` ⇒ NV CHƯA được cấp tài khoản: không tạo `User` nào cả.
        // (Không thể tạo `User` với `EmployeeCode = null` — cột này bắt buộc.)
        if (employeeCode is not null)
            db.Users.Add(new User { Id = UserId, Username = "nv001", PasswordHash = "x", EmployeeCode = employeeCode });
        db.Positions.Add(new Position { Id = PositionId, Name = "Công nhân" });
        db.EmployeeInformations.Add(new EmployeeInformation
        {
            Id = EmpId,
            UserId = employeeCode is null ? null : UserId,
            FullName = "Nguyễn Văn A",
            CitizenId = CitizenId,
            PositionId = PositionId,
            Gender = Gender.Male,
        });
        db.ShiftTemplates.Add(new ShiftTemplate
        {
            Id = ShiftId,
            Name = "Ca hành chính",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0),
        });
        db.Contracts.Add(new Contract
        {
            Id = ContractId,
            ContractNo = "HD-001",
            EmployeeInformationId = EmpId,
            Kind = ContractEnums.ContractKind.LaborFullTime,
            Status = ContractEnums.ContractStatus.Active,
            EffectiveFrom = new DateTime(2025, 1, 1),
            EffectiveTo = null,
            SalaryGradeId = 1,
            SalaryAmountSnapshot = Salary,
            SocialInsuranceFlag = socialFlag,
            HealthInsuranceFlag = healthFlag,
            UnemploymentInsuranceFlag = unemploymentFlag,
        });

        await db.SaveChangesAsync();
        return db;
    }

    public const string CitizenId = "001234567890";

    /// <summary>Bản ghi chấm công ca CHÍNH (<c>ShiftTemplateId</c> non-null, <c>OvertimeRequestId</c> null).</summary>
    public static AttendanceRecord ShiftRecord(int day, decimal dayHours = 8m, decimal nightHours = 0m) => new()
    {
        EmployeeInformationId = EmpId,
        WorkDate = D(day),
        ShiftTemplateId = ShiftId,
        Status = AttendanceStatus.Present,
        DayHours = dayHours,
        NightHours = nightHours,
        CreatedByUserId = UserId,
    };

    /// <summary>Bản ghi chấm công cho ca OT (<c>OvertimeRequestId</c> non-null, <c>ShiftTemplateId</c> null).</summary>
    public static AttendanceRecord OtRecord(int day, int otRequestId, decimal dayHours = 4m, decimal nightHours = 0m) => new()
    {
        EmployeeInformationId = EmpId,
        WorkDate = D(day),
        ShiftTemplateId = null,
        OvertimeRequestId = otRequestId,
        Status = AttendanceStatus.Present,
        DayHours = dayHours,
        NightHours = nightHours,
        CreatedByUserId = UserId,
    };

    public static MPS.Domain.Entities.Attendance.OvertimeRequest ApprovedOt(
        int id, int day, TimeOnly start, TimeOnly end) => new()
    {
        Id = id,
        EmployeeInformationId = EmpId,
        WorkDate = D(day),
        OtStartTime = start,
        OtEndTime = end,
        OtHours = (decimal)(end - start).TotalHours,
        OtDayHours = (decimal)(end - start).TotalHours,
        OtNightHours = 0m,
        Status = OtRequestStatus.Approved,
        CreatedByUserId = UserId,
    };

    public static EmployeeShiftAssignment ShiftAssignment(int day) => new()
    {
        EmployeeInformationId = EmpId,
        WorkDate = D(day),
        ShiftTemplateId = ShiftId,
        CreatedByUserId = UserId,
    };

    /// <summary>Chạy collector trên <paramref name="db"/> và trả input của NV duy nhất.</summary>
    public static async Task<PayrollEmployeeInputs> CollectAsync(
        AppDbContext db, IComplianceLookupService? compliance = null) =>
        (await CollectFullAsync(db, compliance)).Employees.Single();

    /// <summary>
    /// Như <see cref="CollectAsync"/> nhưng trả TOÀN BỘ kết quả — cần cho các test đọc
    /// <c>ComplianceRefs</c> / <c>ComplianceNotes</c> chứ không chỉ số của nhân viên.
    /// </summary>
    public static async Task<PayrollCollectionResult> CollectFullAsync(
        AppDbContext db, IComplianceLookupService? compliance = null)
    {
        var lookup = compliance ?? new FakeComplianceLookup();
        var collector = new PayrollDataCollector(
            db, lookup, new DayTypeResolver(db), new OvertimeMultiplierCalculator(lookup));

        return await collector.CollectAsync(
            new PayrollCollectionRequest(Year, Month, PayDate, StandardDays, new[] { EmpId }),
            CancellationToken.None);
    }
}

/// <summary>Số liệu compliance cố định — tách khỏi DB để test đo đúng logic của collector.</summary>
internal sealed class FakeComplianceLookup : IComplianceLookupService
{
    /// <summary>Cho phép test đổi riêng hệ số trần BHYT mà giữ nguyên trần BHXH.</summary>
    public int? HealthCapMultiplierOverride { get; set; }

    /// <summary>
    /// [Mục 1] Cho phép test trả hằng số luật lao động KHÁC NHAU theo ngày, dựng lại tình huống
    /// luật đổi giữa kỳ. Null ⇒ cả kỳ dùng một mức (hành vi cũ).
    /// </summary>
    public Func<DateOnly, LaborLawConstantsSnapshot>? LaborLawByDate { get; set; }

    private SocialInsuranceRateSnapshot CurrentSi =>
        HealthCapMultiplierOverride is int m ? Si with { HealthCapMultiplier = m } : Si;

    private static readonly SocialInsuranceRateSnapshot Si = new(
        VersionId: 1, ValidFrom: new DateOnly(2026, 1, 1),
        EmployeeSocialPercent: 8m, EmployerSocialPercent: 17m,
        EmployerOccupationalDiseasePercent: 0.5m,
        EmployeeHealthPercent: 1.5m, EmployerHealthPercent: 3m,
        EmployeeUnemploymentPercent: 1m, EmployerUnemploymentPercent: 1m,
        EmployerUnionFundPercent: 2m,
        ReferenceAmount: 2_340_000m, SocialCapMultiplier: 20, HealthCapMultiplier: 20,
        UnemploymentCapMultiplier: 20, LegalReference: "test",
        Funds: new[] { new SocialInsuranceFundEntrySnapshot("HT", "Hưu trí", 8m, 14m) },
        EligibilityMode: "ByContractSubtype", EligibleContractSubtypes: Array.Empty<string>(),
        MinTenureMonths: null);

    private static readonly PitRegulationSnapshot Pit = new(
        VersionId: 1, ValidFrom: new DateOnly(2026, 1, 1),
        PersonalDeductionAmount: 11_000_000m, DependentDeductionAmount: 4_400_000m,
        ShortTermContractMaxMonths: 3, ShortTermFlatRatePercent: 10m,
        ShortTermTaxExemptMonthlyIncome: 2_000_000m,
        Brackets: new[] { new PitBracketSnapshot(1, 0m, 5_000_000m, 5m) },
        LegalReference: "test");

    private static readonly MinimumWageSnapshot MinWage = new(
        VersionId: 1, ValidFrom: new DateOnly(2026, 1, 1), AppliedRegionCode: "I",
        Regions: new[] { new MinimumWageRegionSnapshot("I", 4_960_000m) }, LegalReference: "test");

    /// <summary>Mức luật lao động nền — test có thể nhân bản rồi chỉnh vài trường bằng <c>with</c>.</summary>
    public static LaborLawConstantsSnapshot BaseLabor => Labor;

    private static readonly LaborLawConstantsSnapshot Labor = new(
        VersionId: 1, ValidFrom: new DateOnly(2026, 1, 1),
        ProbationSalaryMinPercent: 85m, OvertimeNightExtraPercent: 20m,
        NightWorkSurchargePercent: 30m, OvertimeNormalUpliftPercent: 50m,
        NightStartTime: new TimeOnly(22, 0), NightEndTime: new TimeOnly(6, 0),
        StandardWorkingHoursPerDay: 8m, MaxRegularHoursPerWeek: 48m,
        StandardWorkingDaysPerMonth: 26m, AnnualLeaveMinDays: 12,
        WorkRateNormalFloor: 100m, WorkRateWeeklyRestFloor: 200m, WorkRateHolidayFloor: 300m,
        OvertimeMaxHoursPerDay: 12m, OvertimeMaxHoursPerMonth: 40m, OvertimeMaxHoursPerYear: 200,
        LegalReference: "test");

    public Task<SocialInsuranceRateSnapshot> GetActiveSocialInsuranceAsync(CancellationToken ct) => Task.FromResult(CurrentSi);
    public Task<PitRegulationSnapshot> GetActivePitAsync(CancellationToken ct) => Task.FromResult(Pit);
    public Task<MinimumWageSnapshot> GetActiveMinimumWageAsync(CancellationToken ct) => Task.FromResult(MinWage);
    public Task<AllowanceExemptThresholdSnapshot?> GetActiveAllowanceThresholdAsync(int allowanceTypeId, CancellationToken ct) =>
        Task.FromResult<AllowanceExemptThresholdSnapshot?>(null);
    public Task<LaborLawConstantsSnapshot> GetActiveLaborLawConstantsAsync(CancellationToken ct) => Task.FromResult(Labor);

    public Task<SocialInsuranceRateSnapshot> GetSocialInsuranceAtAsync(DateOnly payDate, CancellationToken ct) => Task.FromResult(CurrentSi);
    public Task<PitRegulationSnapshot> GetPitAtAsync(DateOnly payDate, CancellationToken ct) => Task.FromResult(Pit);
    public Task<MinimumWageSnapshot> GetMinimumWageAtAsync(DateOnly payDate, CancellationToken ct) => Task.FromResult(MinWage);
    public Task<AllowanceExemptThresholdSnapshot?> GetAllowanceThresholdAtAsync(int allowanceTypeId, DateOnly payDate, CancellationToken ct) =>
        Task.FromResult<AllowanceExemptThresholdSnapshot?>(null);
    public Task<LaborLawConstantsSnapshot> GetLaborLawConstantsAtAsync(DateOnly payDate, CancellationToken ct) =>
        Task.FromResult(LaborLawByDate?.Invoke(payDate) ?? Labor);
}
