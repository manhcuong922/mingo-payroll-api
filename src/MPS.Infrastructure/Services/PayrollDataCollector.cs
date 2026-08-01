using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Application.Features.Payroll.Common;
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
    private readonly IDayTypeResolver _dayTypeResolver;
    private readonly IOvertimeMultiplierCalculator _otMultiplier;

    public PayrollDataCollector(
        AppDbContext db,
        IComplianceLookupService compliance,
        IDayTypeResolver dayTypeResolver,
        IOvertimeMultiplierCalculator otMultiplier)
    {
        _db = db;
        _compliance = compliance;
        _dayTypeResolver = dayTypeResolver;
        _otMultiplier = otMultiplier;
    }

    private enum CongCategory { Official, Probation, Apprentice }

    private static readonly IReadOnlyDictionary<DateOnly, DayType> EmptyDayTypes =
        new Dictionary<DateOnly, DayType>();

    // #64/[D15] — 4 mã có cột riêng trên bảng lương; mọi mã khác gom vào cột "Khác" (AllowanceOther).
    private static readonly HashSet<string> HardcodedAllowanceCodes =
        new(StringComparer.OrdinalIgnoreCase) { "HOUSING", "PHONE", "FUEL", "MEAL" };

    /// <summary>
    /// PRORATE-02 — cơm phải tách riêng khỏi khối "ngoại lệ" chung: tiền cơm luôn ở lại cột
    /// <c>COM</c> (dù khai ngoại lệ hay không) để giữ suất miễn thuế cơm <c>MIN(COM, ngưỡng)</c>.
    /// </summary>
    private const string MealCode = "MEAL";

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

        // [D12] — phụ lục ĐÃ KÍCH HOẠT có đổi lương: mỗi cái là một mốc lương mới của hợp đồng.
        // Chỉ lấy phụ lục có hiệu lực KHÔNG MUỘN HƠN đầu kỳ để kỳ cũ giữ nguyên mức lương cũ.
        var contractIds = contracts.Select(c => c.Id).ToList();
        var periodStartUtc = periodStart.ToDateTime(TimeOnly.MinValue);
        var salaryChangesByContract = (await _db.ContractAppendices.AsNoTracking()
            .Where(a => contractIds.Contains(a.ContractId)
                && a.Status == ContractEnums.AppendixStatus.Active
                && a.SalaryAmountSnapshot != null
                && a.EffectiveFrom <= periodStartUtc)
            .Select(a => new { a.ContractId, a.EffectiveFrom, a.SalaryAmountSnapshot })
            .ToListAsync(ct))
            .GroupBy(a => a.ContractId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(a => a.EffectiveFrom).First().SalaryAmountSnapshot!.Value);

        // ⭐ A1 — chỉ lấy bản ghi chấm công CA CHÍNH (`OvertimeRequestId == null`).
        // HR chấm công 2 lần cho ngày có tăng ca (một cho ca chính, một cho ca OT) đúng như hệ thống
        // thiết kế — `AttendanceRecord` có invariant exactly-one-of (ShiftTemplateId, OvertimeRequestId).
        // Trước đây vòng lặp bên dưới nuốt cả hai loại nên bản ghi ca OT bị tính 3 lần thừa:
        //   (1) `DayWeight` cộng thêm 1 ngày công khống ⇒ phồng CongTinhCom/CongChinhThuc, kéo theo
        //       thu nhập phân bổ theo công, tiền cơm và nền đóng BHXH;
        //   (2) `dayHours/nightHours` nuốt giờ OT ⇒ trả hai lần (đã trả qua cột Tăng ca);
        //   (3) `workSegments` sinh phụ trội loại-ngày lần nữa ⇒ OT ngày nghỉ/lễ được ~300% thay vì 200%.
        // Tiền OT vẫn trả đủ qua `ComputeOvertimePayAsync` (đọc từ `OvertimeRequests` đã duyệt).
        // Cùng quy ước với `GetAttendanceSummaryQueryHandler.ComputeEquivalentHours` — nơi đã tách 2 nhánh.
        var attByEmp = (await _db.AttendanceRecords.AsNoTracking()
            .Where(a => empIds.Contains(a.EmployeeInformationId) && a.WorkDate >= periodStart && a.WorkDate <= periodEnd
                && a.OvertimeRequestId == null)
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

            var ots = otByEmp.GetValueOrDefault(e.Id) ?? new List<Domain.Entities.Attendance.OvertimeRequest>();

            // ⭐ BS-2: loại ngày cần cho CẢ giờ thường (phụ trội nghỉ tuần/lễ) lẫn OT — resolve 1 lần
            // cho cả kỳ, dùng chung, tránh gọi 2 lượt.
            var dayTypeByDate = records.Count > 0 || ots.Count > 0
                ? await _dayTypeResolver.ResolveRangeAsync(e.Id, periodStart, periodEnd, ct)
                : EmptyDayTypes;

            decimal congCom = 0, congThuViec = 0, congChinhThuc = 0, congHocViec = 0, dayHours = 0, nightHours = 0;
            decimal weeklyRestHours = 0, holidayHours = 0;
            var workSegments = new List<RegularHoursPremiumCalculator.WorkDaySegment>(records.Count);

            foreach (var r in records)
            {
                // ⭐ #25: chỉ tính ngày công NẰM TRONG kỳ hiệu lực của một hợp đồng. Trước đây mọi
                // AttendanceRecord trong kỳ đều được tính, nên NV vào/ra giữa tháng vẫn hưởng đủ công
                // cả tháng (và NV không có HĐ hiệu lực vẫn được tính công) ⇒ trả lương sai.
                if (!IsCoveredByContract(empContracts, r.WorkDate)) continue;

                // Loại ngày phải resolve TRƯỚC khi tính trọng số công: ngày nghỉ CÓ LƯƠNG cũng là
                // ngày công được trả (xem DayWeight).
                var dayType = dayTypeByDate.GetValueOrDefault(r.WorkDate, DayType.Normal);

                var weight = DayWeight(r.Status, dayType);
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

                // BS-2 phần A: ca CHÍNH THỨC (không qua đơn OT) vẫn có thể rơi vào nghỉ tuần/lễ.
                workSegments.Add(new RegularHoursPremiumCalculator.WorkDaySegment(dayType, r.DayHours, r.NightHours));
                switch (dayType)
                {
                    case DayType.WeeklyRestDay:
                        weeklyRestHours += r.DayHours + r.NightHours; break;
                    case DayType.PublicHoliday:
                    case DayType.PaidLeaveDay:
                        holidayHours += r.DayHours + r.NightHours; break;
                }
            }

            // [D12] — mức lương áp cho kỳ: nếu HĐ đã có phụ lục đổi lương hiệu lực từ trước đầu kỳ
            // thì dùng mức của phụ lục gần nhất, không thì dùng mức đóng băng lúc ký HĐ. Nhờ vậy
            // tính lại một kỳ CŨ vẫn ra đúng mức lương của thời điểm đó.
            var mucLuong = activeContract is null
                ? 0m
                : salaryChangesByContract.TryGetValue(activeContract.Id, out var changed)
                    ? changed
                    : activeContract.SalaryAmountSnapshot;
            var hourlyRate = req.StandardWorkingDays > 0 && labor.StandardWorkingHoursPerDay > 0
                ? mucLuong / (req.StandardWorkingDays * labor.StandardWorkingHoursPerDay) : 0m;

            // ⭐ BS-2: hệ số OT theo LOẠI NGÀY (Đ.98) — trước đây dùng cứng 1.5/2.0 cho mọi ngày, bỏ qua
            // cuối tuần (200%) và lễ (300%) ⇒ ai tăng ca ngày lễ bị trả thiếu. Với ngày THƯỜNG, hệ số động
            // ra đúng 1.5 (ngày) / 2.0 (đêm) như cũ nên không đổi số cho các kỳ chỉ có OT ngày thường.
            var (tangCa, chenhLech) = await ComputeOvertimePayAsync(
                e.Id, ots, hourlyRate, labor, dayTypeByDate, ct);

            // ⭐ BS-2 phần A: giờ THƯỜNG ngày nghỉ/lễ chỉ được trả 100% qua cột Thu nhập ⇒ trả phần
            // còn thiếu (200%/300%); cộng phụ cấp làm đêm 30% mà trước đây KHÔNG cột nào trả.
            var premium = RegularHoursPremiumCalculator.Accumulate(workSegments, hourlyRate, labor);

            decimal finalScore = 0m, coefficient = 1.0m;
            if (e.UserId.HasValue && scoreByUser.TryGetValue(e.UserId.Value, out var sc) && sc.HasValue)
            {
                finalScore = sc.Value;
                var mapped = prodMap.FirstOrDefault(p => finalScore >= p.MinScore && finalScore <= p.MaxScore);
                if (mapped is not null) coefficient = mapped.Coefficient;
            }

            decimal AllowanceByCode(string code) =>
                activeContract?.ContractAllowances.Where(a => a.AllowanceType.Code == code).Sum(a => a.Amount) ?? 0m;

            // #64/[D15] — phụ cấp mã KHÁC 4 mã cứng (HOUSING/PHONE/FUEL/MEAL) trước đây RƠI mất khỏi
            // bảng lương (không cột nào nhận). Gom về cột "Khác" để tiền vào Thu nhập, không biến mất.
            decimal AllowanceOther() =>
                activeContract?.ContractAllowances
                    .Where(a => !HardcodedAllowanceCodes.Contains(a.AllowanceType.Code))
                    .Sum(a => a.Amount) ?? 0m;

            // PRORATE-02 — tổng phụ cấp khai NGOẠI LỆ (`AllowanceType.ExcludeFromProration`), TRỪ cơm.
            // Số này được trừ khỏi nền chia theo công rồi cộng nguyên trở lại ở cột Thu nhập, nên cột
            // gốc (Thuê nhà / Điện thoại / Xăng xe / Khác) vẫn hiện đủ số tiền ghi trong hợp đồng.
            decimal AllowanceExcludedFromProration() =>
                activeContract?.ContractAllowances
                    .Where(a => a.AllowanceType.ExcludeFromProration
                        && !string.Equals(a.AllowanceType.Code, MealCode, StringComparison.OrdinalIgnoreCase))
                    .Sum(a => a.Amount) ?? 0m;

            // Cơm tách 2 phần: phần thường (chia theo công) và phần khai ngoại lệ (cộng nguyên) —
            // cả hai cùng đổ về cột COM nên suất cơm miễn thuế vẫn tính trên tổng.
            decimal MealBy(bool excluded) =>
                activeContract?.ContractAllowances
                    .Where(a => string.Equals(a.AllowanceType.Code, MealCode, StringComparison.OrdinalIgnoreCase)
                        && a.AllowanceType.ExcludeFromProration == excluded)
                    .Sum(a => a.Amount) ?? 0m;

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
            n[ContractAllowanceCom] = MealBy(excluded: false);
            n[ContractAllowanceComNgoaiLe] = MealBy(excluded: true);
            n[ContractAllowanceKhac] = AllowanceOther();
            n[ContractAllowanceNgoaiLe] = AllowanceExcludedFromProration();
            n[CongChuan] = req.StandardWorkingDays;
            n[CongTinhCom] = congCom;
            n[CongThuViec] = congThuViec;
            n[CongChinhThuc] = congChinhThuc;
            n[CongHocViec] = congHocViec;
            n[DayHours] = dayHours;
            n[NightHours] = nightHours;
            n[OtTangCa] = tangCa;
            n[OtChenhLech] = chenhLech;
            n[PremiumDayType] = premium.DayTypePremium;
            n[PremiumNight] = premium.NightPremium;
            n[WeeklyRestHours] = weeklyRestHours;
            n[HolidayHours] = holidayHours;
            n[EvalFinalScore] = finalScore;
            n[PayrollFieldKeys.ProductivityCoefficient] = coefficient;
            // #40 — SỐ SUẤT giảm trừ gia cảnh luôn suy ra từ BẢN GHI người phụ thuộc đã duyệt còn
            // hiệu lực trong kỳ, KHÔNG bao giờ đọc số khai tay `EmployeeInformation.DependentsNo`.
            // Trước đây khi NV chưa có bản ghi nào thì fallback về số khai tay, nên `PUT` một
            // `DependentsNo = 99` (không bản ghi, không chứng từ) tạo ra 99 suất giảm trừ KHỐNG. Theo
            // `CAC-CASE-XAY-RA` §3.2: "không nhận số nhập tay không có căn cứ". Không có bản ghi ⇒ 0.
            n[DependentsCount] = depCountByEmp.TryGetValue(e.Id, out var dc) ? dc : 0m;
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

    /// <summary>
    /// Trọng số ngày công dùng để tính lương.
    ///
    /// <para><b>Ngày nghỉ CÓ LƯƠNG được tính là 1 ngày công.</b> `CAC-CASE-XAY-RA.md` §6: đơn nghỉ
    /// phép được duyệt thì "sinh ngày nghỉ **có lương** trong bảng chấm công, **chảy vào lương với
    /// hệ số ngày phép**".</para>
    ///
    /// <para>Trước đây mọi bản ghi <c>OnLeave</c> đều nhận trọng số 0 — kể cả phép năm/nghỉ ốm đã
    /// trừ quỹ. Hậu quả: nghỉ 5 ngày phép năm thì <b>mất luôn 5 ngày lương</b>, tức phép "có lương"
    /// thực chất không được trả đồng nào; và mọi ngày nghỉ có lương đều có giờ công = 0 nên cũng
    /// không có cột nào khác bù lại.</para>
    ///
    /// <para>Bản thân <c>AttendanceStatus.OnLeave</c> KHÔNG phân biệt được nghỉ có lương hay không
    /// lương, nên phải tra <paramref name="dayType"/> — <c>DayTypeResolver</c> chỉ đánh
    /// <c>PaidLeaveDay</c> khi có đơn nghỉ Approved với <c>IsPaid = true</c>. Nhờ đó nghỉ KHÔNG
    /// lương vẫn giữ trọng số 0, đúng nghiệp vụ.</para>
    /// </summary>
    private static decimal DayWeight(AttendanceStatus status, DayType dayType) => status switch
    {
        AttendanceStatus.Present or AttendanceStatus.Late or AttendanceStatus.EarlyLeave => 1.0m,
        AttendanceStatus.HalfDay => 0.5m,
        AttendanceStatus.OnLeave when dayType == DayType.PaidLeaveDay => 1.0m,
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

    // ⭐ BS-2 — tính tiền OT theo hệ số loại-ngày (Đ.98), thay hệ số cứng 1.5/2.0.
    //   tangCa    = tổng tiền OT               = hourlyRate × Σ(giờ × hệ_số_đầy_đủ)
    //   chenhLech = phần vượt 100% (miễn thuế) = hourlyRate × Σ(giờ × (hệ_số_đầy_đủ − 1))
    // Hệ số lấy từ OvertimeMultiplierCalculator (đã có sẵn nhưng chưa ai dùng), loại ngày từ DayTypeResolver.
    private async Task<(decimal TangCa, decimal ChenhLech)> ComputeOvertimePayAsync(
        int employeeInformationId,
        IReadOnlyList<Domain.Entities.Attendance.OvertimeRequest> ots,
        decimal hourlyRate,
        MPS.Application.Common.Models.Compliance.LaborLawConstantsSnapshot labor,
        IReadOnlyDictionary<DateOnly, DayType> dayTypeByDate,
        CancellationToken ct)
    {
        if (ots.Count == 0 || hourlyRate <= 0m) return (0m, 0m);

        var segments = new List<OvertimePayCalculator.OvertimeSegment>(ots.Count);
        foreach (var ot in ots)
        {
            // Case 5 — resolve loại-ngày cho CẢ ngày đăng ký (D) và ngày kế (D+1): OT vắt nửa đêm sang
            // ngày lễ/nghỉ phải tính hệ số theo đúng ngày lịch của từng phần giờ.
            var dayTypeD = await ResolveDayTypeAsync(employeeInformationId, ot.WorkDate, dayTypeByDate, ct);
            var dayTypeNext = await ResolveDayTypeAsync(employeeInformationId, ot.WorkDate.AddDays(1), dayTypeByDate, ct);

            // Case 3 + 5 — dựng segment từ khung giờ THÔ (không dùng OtDayHours/OtNightHours đã gộp),
            // để tách theo ngày lịch và đặt cờ "đêm sau ngày OT" (Đ.98.3) chính xác.
            segments.AddRange(OvertimeSegmentBuilder.Build(
                ot.OtStartTime, ot.OtEndTime, dayTypeD, dayTypeNext,
                labor.NightStartTime, labor.NightEndTime));
        }

        return OvertimePayCalculator.Accumulate(segments, hourlyRate, _otMultiplier, labor);
    }

    private async Task<DayType> ResolveDayTypeAsync(
        int employeeInformationId, DateOnly date, IReadOnlyDictionary<DateOnly, DayType> cache, CancellationToken ct) =>
        cache.TryGetValue(date, out var dayType)
            ? dayType
            : await _dayTypeResolver.ResolveAsync(employeeInformationId, date, ct);

    // #25 — ngày công chỉ hợp lệ khi rơi vào kỳ hiệu lực của một HĐ (đã loại HĐ Cancelled từ query).
    // Trả true khi employee không có HĐ nào để tránh vô tình xoá sạch công của dữ liệu lịch sử
    // chưa được số hoá hợp đồng — trường hợp đó readiness gate đã cảnh báo ở bước tạo kỳ.
    private static bool IsCoveredByContract(List<Contract> contracts, DateOnly day)
    {
        if (contracts.Count == 0) return true;

        var dayDt = day.ToDateTime(new TimeOnly(12, 0));
        return contracts.Any(c => c.EffectiveFrom <= dayDt && (c.EffectiveTo == null || c.EffectiveTo >= dayDt));
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
