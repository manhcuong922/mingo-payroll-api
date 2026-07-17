using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Enums.Payroll;
using MPS.Infrastructure.Persistence;
using ContractEnums = MPS.Domain.Enums.Contract;
using static MPS.Application.Common.Payroll.PayrollFieldKeys;

namespace MPS.Infrastructure.Services;

/// <inheritdoc />
public sealed class PayrollDataCollector : IPayrollDataCollector
{
    private readonly AppDbContext _db;
    private readonly IComplianceLookupService _compliance;

    public PayrollDataCollector(AppDbContext db, IComplianceLookupService compliance)
    {
        _db = db;
        _compliance = compliance;
    }

    private enum CongCategory { Official, Probation, Apprentice }

    public async Task<PayrollCollectionResult> CollectAsync(PayrollCollectionRequest req, CancellationToken ct)
    {
        var periodStart = new DateOnly(req.Year, req.Month, 1);
        var periodEnd = new DateOnly(req.Year, req.Month, DateTime.DaysInMonth(req.Year, req.Month));
        var empIds = req.EmployeeInformationIds;

        // ===== Compliance snapshots tại payDate (dùng chung mọi NV) =====
        var si = await _compliance.GetSocialInsuranceAtAsync(req.PayDate, ct);
        var pit = await _compliance.GetPitAtAsync(req.PayDate, ct);
        var minWage = await _compliance.GetMinimumWageAtAsync(req.PayDate, ct);
        var labor = await _compliance.GetLaborLawConstantsAtAsync(req.PayDate, ct);

        var mealAllowanceTypeId = await _db.AllowanceTypes.AsNoTracking()
            .Where(a => a.Code == "MEAL").Select(a => (int?)a.Id).FirstOrDefaultAsync(ct);
        var mealThreshold = mealAllowanceTypeId is int mid
            ? await _compliance.GetAllowanceThresholdAtAsync(mid, req.PayDate, ct)
            : null;

        var bhxhDnRate = si.Funds.Sum(f => f.EmployerPercent) / 100m;
        var bhxhNldRate = si.Funds.Sum(f => f.EmployeePercent) / 100m;
        var bhytDnRate = si.EmployerHealthPercent / 100m;
        var bhytNldRate = si.EmployeeHealthPercent / 100m;
        var bhtnDnRate = si.EmployerUnemploymentPercent / 100m;
        var bhtnNldRate = si.EmployeeUnemploymentPercent / 100m;
        var kpcdRate = si.EmployerUnionFundPercent / 100m;
        var bhxhBhytCap = si.ReferenceAmount * si.SocialCapMultiplier;
        var regionMinWage = minWage.GetAppliedAmount();
        var bhtnCap = regionMinWage * si.UnemploymentCapMultiplier;
        var mealExempt = mealThreshold?.MonthlyExemptAmount ?? 0m;

        // ===== Bảng hệ số năng suất (active) + snapshot JSON =====
        var prodMap = await _db.ProductivityCoefficients.AsNoTracking()
            .Where(p => p.IsActive).OrderBy(p => p.MinScore).ToListAsync(ct);
        var prodSnapshotJson = PayrollJson.Serialize(prodMap.Select(p => new { p.MinScore, p.MaxScore, p.Coefficient, p.Label }));

        // ===== Bulk load per-employee sources =====
        var employees = await _db.EmployeeInformations.AsNoTracking()
            .Include(e => e.Position).Include(e => e.User)
            .Where(e => empIds.Contains(e.Id)).ToListAsync(ct);

        var contracts = await _db.Contracts.AsNoTracking()
            .Include(c => c.ContractAllowances).ThenInclude(a => a.AllowanceType)
            .Where(c => empIds.Contains(c.EmployeeInformationId)
                && c.Status != ContractEnums.ContractStatus.Cancelled)
            .ToListAsync(ct);
        var contractsByEmp = contracts.GroupBy(c => c.EmployeeInformationId).ToDictionary(g => g.Key, g => g.ToList());

        var attByEmp = (await _db.AttendanceRecords.AsNoTracking()
            .Where(a => empIds.Contains(a.EmployeeInformationId) && a.WorkDate >= periodStart && a.WorkDate <= periodEnd)
            .ToListAsync(ct)).GroupBy(a => a.EmployeeInformationId).ToDictionary(g => g.Key, g => g.ToList());

        var otByEmp = (await _db.OvertimeRequests.AsNoTracking()
            .Where(o => empIds.Contains(o.EmployeeInformationId) && o.WorkDate >= periodStart && o.WorkDate <= periodEnd
                && o.Status == OtRequestStatus.Approved)
            .ToListAsync(ct)).GroupBy(o => o.EmployeeInformationId).ToDictionary(g => g.Key, g => g.ToList());

        var depCountByEmp = (await _db.EmployeeDependents.AsNoTracking()
            .Where(d => empIds.Contains(d.EmployeeInformationId) && d.IsActive
                && d.RegisteredFromDate <= periodEnd && (d.RegisteredToDate == null || d.RegisteredToDate >= periodStart))
            .Select(d => d.EmployeeInformationId).ToListAsync(ct))
            .GroupBy(x => x).ToDictionary(g => g.Key, g => (decimal)g.Count());

        var userIds = employees.Where(e => e.UserId.HasValue).Select(e => e.UserId!.Value).ToList();
        var scoreByUser = (await _db.MonthlyEvaluations.AsNoTracking()
            .Where(m => userIds.Contains(m.EmployeeUserId) && m.PeriodYear == req.Year && m.PeriodMonth == req.Month
                && m.Status == MPS.Domain.Enums.Evaluation.EvaluationStatus.Approved)
            .Select(m => new { m.EmployeeUserId, m.FinalScore }).ToListAsync(ct))
            .GroupBy(m => m.EmployeeUserId).ToDictionary(g => g.Key, g => g.First().FinalScore);

        var monthLabel = $"{req.Month:D2}/{req.Year}";
        var result = new List<PayrollEmployeeInputs>();

        foreach (var e in employees)
        {
            var empContracts = contractsByEmp.GetValueOrDefault(e.Id) ?? new List<Contract>();
            var activeContract = PickContract(empContracts, periodStart, periodEnd);
            var records = attByEmp.GetValueOrDefault(e.Id) ?? new List<Domain.Entities.Attendance.AttendanceRecord>();

            decimal congCom = 0, congThuViec = 0, congChinhThuc = 0, congHocViec = 0, dayHours = 0, nightHours = 0;
            foreach (var r in records)
            {
                var weight = DayWeight(r.Status);
                if (weight > 0)
                {
                    congCom += weight;
                    switch (ResolveCategory(empContracts, r.WorkDate, activeContract))
                    {
                        case CongCategory.Probation: congThuViec += weight; break;
                        case CongCategory.Apprentice: congHocViec += weight; break;
                        default: congChinhThuc += weight; break;
                    }
                }
                dayHours += r.DayHours;
                nightHours += r.NightHours;
            }

            var mucLuong = activeContract?.SalaryAmountSnapshot ?? 0m;
            var hourlyRate = req.StandardWorkingDays > 0 && labor.StandardWorkingHoursPerDay > 0
                ? mucLuong / (req.StandardWorkingDays * labor.StandardWorkingHoursPerDay) : 0m;
            var ots = otByEmp.GetValueOrDefault(e.Id) ?? new List<Domain.Entities.Attendance.OvertimeRequest>();
            decimal otDay = ots.Sum(o => o.OtDayHours), otNight = ots.Sum(o => o.OtNightHours);
            var tangCa = Math.Round(hourlyRate * (otDay * 1.5m + otNight * 2.0m), 0, MidpointRounding.AwayFromZero);
            var chenhLech = Math.Round(hourlyRate * (otDay * 0.5m + otNight * 1.0m), 0, MidpointRounding.AwayFromZero);

            decimal finalScore = 0m, coefficient = 1.0m;
            if (e.UserId.HasValue && scoreByUser.TryGetValue(e.UserId.Value, out var sc) && sc.HasValue)
            {
                finalScore = sc.Value;
                var mapped = prodMap.FirstOrDefault(p => finalScore >= p.MinScore && finalScore <= p.MaxScore);
                if (mapped is not null) coefficient = mapped.Coefficient;
            }

            decimal AllowanceByCode(string code) =>
                activeContract?.ContractAllowances.Where(a => a.AllowanceType.Code == code).Sum(a => a.Amount) ?? 0m;

            var inputs = new PayrollEmployeeInputs { EmployeeInformationId = e.Id };
            var n = inputs.Numbers; var t = inputs.Texts;

            t[EmployeeCode] = e.User?.EmployeeCode ?? e.CitizenId;
            t[EmployeeFullName] = e.FullName;
            t[EmployeePosition] = e.Position?.Name ?? string.Empty;
            t[EmployeeBankAccount] = e.BankNumber ?? string.Empty;
            t[EmployeeJoinDate] = empContracts.Count > 0 ? empContracts.Min(c => c.EffectiveFrom).ToString("dd/MM/yyyy") : string.Empty;
            t[PeriodMonthLabel] = monthLabel;
            t[ContractInsuranceFlag] = ResolveInsuranceFlag(activeContract);
            t[ContractTaxFlag] = ResolveTaxFlag(activeContract);

            n[ContractMucLuong] = mucLuong;
            n[ContractAllowanceThueNha] = AllowanceByCode("HOUSING");
            n[ContractAllowanceDienThoai] = AllowanceByCode("PHONE");
            n[ContractAllowanceXangXe] = AllowanceByCode("FUEL");
            n[ContractAllowanceCom] = AllowanceByCode("MEAL");
            n[CongChuan] = req.StandardWorkingDays;
            n[CongTinhCom] = congCom;
            n[CongThuViec] = congThuViec;
            n[CongChinhThuc] = congChinhThuc;
            n[CongHocViec] = congHocViec;
            n[DayHours] = dayHours;
            n[NightHours] = nightHours;
            n[OtTangCa] = tangCa;
            n[OtChenhLech] = chenhLech;
            n[EvalFinalScore] = finalScore;
            n[PayrollFieldKeys.ProductivityCoefficient] = coefficient;
            n[DependentsCount] = depCountByEmp.TryGetValue(e.Id, out var dc) ? dc : e.DependentsNo;
            n[BonusThuongDuAn] = 0m;
            n[UnionFee] = 0m;
            n[BhxhDnRate] = bhxhDnRate;
            n[BhxhNldRate] = bhxhNldRate;
            n[BhytDnRate] = bhytDnRate;
            n[BhytNldRate] = bhytNldRate;
            n[BhtnDnRate] = bhtnDnRate;
            n[BhtnNldRate] = bhtnNldRate;
            n[KpcdRate] = kpcdRate;
            n[BhxhBhytCap] = bhxhBhytCap;
            n[BhtnCap] = bhtnCap;
            n[PersonalDeduction] = pit.PersonalDeductionAmount;
            n[DependentDeduction] = pit.DependentDeductionAmount;
            n[MealExemptThreshold] = mealExempt;

            result.Add(inputs);
        }

        var refs = new List<PayrollComplianceRef>
        {
            Ref(ComplianceVersionType.SocialInsurance, si.VersionId, si.ValidFrom, si.LegalReference, "Trần đóng BHXH/BHYT", bhxhBhytCap),
            Ref(ComplianceVersionType.Pit, pit.VersionId, pit.ValidFrom, pit.LegalReference, "Giảm trừ bản thân", pit.PersonalDeductionAmount),
            Ref(ComplianceVersionType.MinimumWage, minWage.VersionId, minWage.ValidFrom, minWage.LegalReference, "Lương tối thiểu vùng áp dụng", regionMinWage),
            Ref(ComplianceVersionType.LaborLawConstants, labor.VersionId, labor.ValidFrom, labor.LegalReference, "Công chuẩn/tháng (luật)", labor.StandardWorkingDaysPerMonth),
        };
        if (mealThreshold is not null)
        {
            var r = Ref(ComplianceVersionType.AllowanceThreshold, mealThreshold.VersionId, mealThreshold.ValidFrom,
                mealThreshold.LegalReference, "Cơm miễn thuế", mealExempt);
            r.ScopeId = mealAllowanceTypeId;
            refs.Add(r);
        }

        return new PayrollCollectionResult
        {
            Employees = result,
            ComplianceRefs = refs,
            FunctionContext = new PayrollFunctionContext { PitBrackets = pit.Brackets },
            ProductivitySnapshotJson = prodSnapshotJson,
        };
    }

    private static PayrollComplianceRef Ref(ComplianceVersionType type, int versionId, DateOnly validFrom,
        string legalRef, string headlineLabel, decimal headlineValue) => new()
    {
        VersionType = type,
        VersionId = versionId,
        ValidFrom = validFrom,
        LegalReference = legalRef,
        HeadlineLabel = headlineLabel,
        HeadlineValue = headlineValue,
    };

    private static decimal DayWeight(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present or AttendanceStatus.Late or AttendanceStatus.EarlyLeave => 1.0m,
        AttendanceStatus.HalfDay => 0.5m,
        _ => 0m,
    };

    private static Contract? PickContract(List<Contract> contracts, DateOnly start, DateOnly end)
    {
        var startDt = start.ToDateTime(TimeOnly.MinValue);
        var endDt = end.ToDateTime(TimeOnly.MaxValue);
        return contracts
            .Where(c => c.EffectiveFrom <= endDt && (c.EffectiveTo == null || c.EffectiveTo >= startDt))
            .OrderByDescending(c => c.Status == ContractEnums.ContractStatus.Active)
            .ThenByDescending(c => c.EffectiveFrom)
            .FirstOrDefault();
    }

    private static CongCategory ResolveCategory(List<Contract> contracts, DateOnly day, Contract? fallback)
    {
        var dayDt = day.ToDateTime(new TimeOnly(12, 0));
        var c = contracts.FirstOrDefault(x => x.EffectiveFrom <= dayDt && (x.EffectiveTo == null || x.EffectiveTo >= dayDt))
                ?? fallback;
        if (c is null) return CongCategory.Official;
        if (c.Kind == ContractEnums.ContractKind.VocationalTraining) return CongCategory.Apprentice;
        if (c.Kind == ContractEnums.ContractKind.Probation || c.ContractSubtype == ContractEnums.ContractSubtype.Probation)
            return CongCategory.Probation;
        return CongCategory.Official;
    }

    private static string ResolveInsuranceFlag(Contract? c)
    {
        if (c is null) return InsuranceFlags.Full;
        if (!c.SocialInsuranceFlag && !c.HealthInsuranceFlag && !c.UnemploymentInsuranceFlag) return InsuranceFlags.NoInsurance;
        if (c.HealthInsuranceFlag && !c.SocialInsuranceFlag && !c.UnemploymentInsuranceFlag) return InsuranceFlags.HealthOnly;
        return InsuranceFlags.Full;
    }

    private static string ResolveTaxFlag(Contract? c) => c?.TaxPolicy switch
    {
        ContractEnums.TaxPolicy.FlatRate10Percent => TaxFlags.Khoan10,
        ContractEnums.TaxPolicy.Exempt => TaxFlags.CamKet,
        _ => TaxFlags.Progressive,
    };
}
