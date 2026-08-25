using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Services;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using AttendanceRecordEntity = MPS.Domain.Entities.Attendance.AttendanceRecord;
using EmployeeInformationEntity = MPS.Domain.Entities.EmployeeInformation;

namespace MPS.Application.Features.Attendance.Attendance.Queries;

/// <summary>
/// Bảng chấm công tháng — aggregate theo NV × DayType × giờ × hệ số.
/// - Status-based counts (Present/Absent/Late/HalfDay/OnLeave/EarlyLeave/Total) — cho Báo cáo cũ.
/// - DayType-based aggregations (Normal/WeeklyRest/Holiday/PaidLeave) — cho Bảng chấm công tháng.
///   ⭐ CONG-MONTHLY-2026-08 — công quy đổi qua <see cref="WorkDayCongCalculator"/>, ĐÚNG hàm mà
///   bảng lương dùng: cộng GIỜ công được công nhận của cả kỳ theo từng loại ngày, rồi mới chia giờ
///   chuẩn/ngày và làm tròn LÊN bội số 0,5. Làm tròn từng nhóm loại-ngày, cùng quy ước "làm tròn
///   từng nhóm" mà bảng lương áp cho chính thức / thử việc / học việc.
///   Trước đây màn này tự đọc <c>AttendanceStatus</c> (HalfDay = 0,5; còn lại = 1,0) nên ca 8h về
///   sớm 2h ra 1,0 công ở đây nhưng 0,75 công trên bảng lương — hai con số khác nhau cho cùng một
///   tháng, không ai đối chiếu nổi.
/// - Quy đổi giờ, chia 2 nhánh:
///   • Non-OT (record không có OvertimeRequestId) — dùng ShiftRate (AttendanceRuleVersion):
///       - Normal day:      DayHours × Rate% / 100 + NightHours × (Rate% + NightSurcharge%) / 100
///       - WeeklyRest/Holiday/PaidLeave: (DayHours + NightHours) × Rate% / 100  (không cộng surcharge)
///   • OT (record có OvertimeRequestId) — dùng OvertimeMultiplierCalculator (LaborLaw floor + Điều 98.1/98.3):
///       DayHours × OtCalc(dayType, isNight=false) + NightHours × OtCalc(dayType, isNight=true, hasPrevDayOt)
///     hasPrevDayOt = true nếu cùng NV, cùng WorkDate có bất kỳ record OT nào chứa DayHours > 0.
///
/// Perf: paginate ở tầng NV. DayType resolve per-NV. Records batch 1 query cho toàn page.
/// LaborLaw snapshot cache theo distinct WorkDate (chỉ prefetch ngày cần: NightHours>0 hoặc là OT).
/// </summary>
public class GetAttendanceSummaryQueryHandler
    : IRequestHandler<GetAttendanceSummaryQuery, PagedResult<EmployeeAttendanceSummaryDto>>
{
    private const int MaxPageSize = 1000;
    private const int DefaultPageSize = 20;

    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IAttendanceRecordRepository _attendanceRepository;
    private readonly IDayTypeResolver _dayTypeResolver;
    private readonly IAttendanceRateResolver _rateResolver;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly IOvertimeMultiplierCalculator _otCalculator;
    private readonly IAttendanceSettingRepository _settingRepository;
    private readonly ILateEarlyRequestRepository _lateEarlyRepository;

    public GetAttendanceSummaryQueryHandler(
        IEmployeeInformationRepository employeeRepository,
        IAttendanceRecordRepository attendanceRepository,
        IDayTypeResolver dayTypeResolver,
        IAttendanceRateResolver rateResolver,
        IComplianceLookupService complianceLookup,
        IOvertimeMultiplierCalculator otCalculator,
        IAttendanceSettingRepository settingRepository,
        ILateEarlyRequestRepository lateEarlyRepository)
    {
        _employeeRepository = employeeRepository;
        _attendanceRepository = attendanceRepository;
        _dayTypeResolver = dayTypeResolver;
        _rateResolver = rateResolver;
        _complianceLookup = complianceLookup;
        _otCalculator = otCalculator;
        _settingRepository = settingRepository;
        _lateEarlyRepository = lateEarlyRepository;
    }

    public async Task<PagedResult<EmployeeAttendanceSummaryDto>> Handle(
        GetAttendanceSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? DefaultPageSize : Math.Min(request.Size, MaxPageSize);

        // Tầng 1 — page NV theo filter (departmentId + optional empId)
        var (employees, total) = await LoadEmployeesAsync(request, page, size, cancellationToken);
        if (employees.Count == 0)
            return new PagedResult<EmployeeAttendanceSummaryDto>(
                Array.Empty<EmployeeAttendanceSummaryDto>(), total, page, size);

        // Tầng 2 — batch records cho toàn page (1 query)
        var recordsByEmp = await _attendanceRepository.GetForSummaryByEmployeesAsync(
            employees.Select(e => e.Id).ToList(),
            request.FromDate,
            request.ToDate,
            cancellationToken);

        // Tầng 2b — prefetch versions cho toàn bộ rule liên quan (avoid N+1 khi resolve rate per record)
        var allRecords = recordsByEmp.Values.SelectMany(list => list).ToList();
        var ruleIds = allRecords
            .Select(r => r.ShiftTemplate?.AppliedAttendanceRuleId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        await _rateResolver.PrefetchAsync(ruleIds, cancellationToken);

        // Tầng 2c — cache LaborLaw snapshot theo distinct WorkDate (versioned by date).
        // Prefetch chỉ những ngày cần: có NightHours > 0 (surcharge non-OT) hoặc là record OT (calc dùng snapshot).
        var laborLawByDate = await BuildLaborLawMapAsync(allRecords, cancellationToken);

        // Tầng 2d — nền để tính TRỌNG SỐ CÔNG giống hệt bảng lương: grace của cấu hình chấm công,
        // giờ chuẩn/ngày của luật (chỉ dùng khi bản ghi mất ca), và các ngày đã có đơn đi muộn/về
        // sớm được duyệt (hưởng trọn công). Đọc 1 lượt cho cả page, không hỏi lẻ từng ngày.
        var congContext = await BuildCongContextAsync(
            employees.Select(e => e.Id).ToList(), request, cancellationToken);

        // Tầng 3 — mỗi NV: resolve DayType range (prefetch, no N+1 trong resolver) + aggregate
        var results = new List<EmployeeAttendanceSummaryDto>(employees.Count);
        foreach (var emp in employees)
        {
            var dayMap = await _dayTypeResolver.ResolveRangeAsync(
                emp.Id, request.FromDate, request.ToDate, cancellationToken);

            var empRecords = recordsByEmp.TryGetValue(emp.Id, out var list)
                ? list
                : Array.Empty<AttendanceRecordEntity>();

            results.Add(Aggregate(
                emp, empRecords, dayMap, _rateResolver, _otCalculator, laborLawByDate, congContext));
        }

        return new PagedResult<EmployeeAttendanceSummaryDto>(results, total, page, size);
    }

    /// <summary>
    /// Nền dùng chung cho <see cref="WorkDayCongCalculator"/> trên toàn page.
    ///
    /// <para><c>StandardHoursPerDay</c> gánh hai vai — trần giờ khi bản ghi mất ca, và mẫu số quy
    /// đổi 1 công — vì cả hai đều là cùng một hằng số luật
    /// (<c>LaborLawConstants.StandardWorkingHoursPerDay</c>), đúng như bên bảng lương.</para>
    /// </summary>
    private readonly record struct CongContext(
        int LateGraceMinutes,
        int EarlyLeaveGraceMinutes,
        decimal StandardHoursPerDay,
        IReadOnlySet<(int EmployeeId, DateOnly WorkDate)> ExcusedDays);

    private async Task<CongContext> BuildCongContextAsync(
        IReadOnlyList<int> employeeIds, GetAttendanceSummaryQuery request, CancellationToken ct)
    {
        // GetAsync (không phải GetOrCreateDefaultAsync): đây là đường READ, không được ghi DB.
        var setting = await _settingRepository.GetAsync(ct);
        var laborLaw = await _complianceLookup.GetActiveLaborLawConstantsAsync(ct);
        var excused = await _lateEarlyRepository.GetApprovedByEmployeesAndRangeAsync(
            employeeIds, request.FromDate, request.ToDate, ct);

        return new CongContext(
            LateGraceMinutes: setting?.LateGraceMinutes ?? AttendanceGraceDefaults.LateGraceMinutes,
            EarlyLeaveGraceMinutes: setting?.EarlyLeaveGraceMinutes ?? AttendanceGraceDefaults.EarlyLeaveGraceMinutes,
            StandardHoursPerDay: laborLaw.StandardWorkingHoursPerDay,
            ExcusedDays: excused.Select(l => (l.EmployeeInformationId, l.WorkDate)).ToHashSet());
    }

    private async Task<IReadOnlyDictionary<DateOnly, LaborLawConstantsSnapshot>> BuildLaborLawMapAsync(
        IReadOnlyList<AttendanceRecordEntity> records, CancellationToken ct)
    {
        var map = new Dictionary<DateOnly, LaborLawConstantsSnapshot>();
        var dates = records
            .Where(r => r.NightHours > 0m || r.OvertimeRequestId is not null)
            .Select(r => r.WorkDate)
            .Distinct();
        foreach (var d in dates)
        {
            map[d] = await _complianceLookup.GetLaborLawConstantsAtAsync(d, ct);
        }
        return map;
    }

    private async Task<(IReadOnlyList<EmployeeInformationEntity> Employees, int Total)> LoadEmployeesAsync(
        GetAttendanceSummaryQuery request,
        int page,
        int size,
        CancellationToken ct)
    {
        // Filter empId: nếu có → chỉ lấy đúng NV đó (ignore department nếu conflict)
        if (request.EmployeeInformationId.HasValue)
        {
            var emp = await _employeeRepository.GetByIdAsync(request.EmployeeInformationId.Value, ct);
            if (emp is null) return (Array.Empty<EmployeeInformationEntity>(), 0);
            if (request.DepartmentId.HasValue && emp.DepartmentId != request.DepartmentId.Value)
                return (Array.Empty<EmployeeInformationEntity>(), 0);
            return (new[] { emp }, 1);
        }

        var (items, total) = await _employeeRepository.GetPagedAsync(
            keyword: null,
            positionId: null,
            hasAccount: null,
            hasActiveUser: true,
            page: page,
            pageSize: size,
            departmentId: request.DepartmentId,
            cancellationToken: ct);

        return (items, total);
    }

    private static EmployeeAttendanceSummaryDto Aggregate(
        EmployeeInformationEntity emp,
        IReadOnlyList<AttendanceRecordEntity> records,
        IReadOnlyDictionary<DateOnly, DayType> dayMap,
        IAttendanceRateResolver rateResolver,
        IOvertimeMultiplierCalculator otCalculator,
        IReadOnlyDictionary<DateOnly, LaborLawConstantsSnapshot> laborLawByDate,
        CongContext congContext)
    {
        var bucket = new Bucket();

        // Precompute: ngày nào NV này đã OT ban ngày → dùng cho hasPrevDayOt của OT đêm.
        var otDayDates = records
            .Where(r => r.OvertimeRequestId is not null && r.DayHours > 0m)
            .Select(r => r.WorkDate)
            .ToHashSet();

        foreach (var r in records)
        {
            // Status-based counts
            switch (r.Status)
            {
                case AttendanceStatus.Present:    bucket.PresentDays++; break;
                case AttendanceStatus.Absent:     bucket.AbsentDays++; break;
                case AttendanceStatus.Late:       bucket.LateDays++; break;
                case AttendanceStatus.HalfDay:    bucket.HalfDays++; break;
                case AttendanceStatus.OnLeave:    bucket.OnLeaveDays++; break;
                case AttendanceStatus.EarlyLeave: bucket.EarlyLeaveDays++; break;
            }
            bucket.TotalRecords++;

            // DayType-based aggregations — bỏ qua Absent (không đi làm không tính ngày công)
            if (r.Status == AttendanceStatus.Absent) continue;

            var dayType = dayMap.TryGetValue(r.WorkDate, out var dt) ? dt : DayType.Normal;

            // ⭐ A1 — TRỌNG SỐ CÔNG chỉ tính cho bản ghi CA CHÍNH. Ngày có tăng ca được chấm 2 bản
            // ghi (ca chính + ca OT); cộng cả hai là ngày đó thành 2 công. Bảng lương đã lọc
            // `OvertimeRequestId == null` từ lâu, màn này thì chưa — đúng nguồn của lệch số.
            // Giờ làm (DayHours/NightHours/OtHours/EquivalentHours) vẫn cộng CẢ HAI, vì đó là giờ thật.
            if (r.OvertimeRequestId is null)
            {
                var recognizedHours = WorkDayCongCalculator.RecognizedHours(new WorkDayInput(
                    Status: r.Status,
                    DayType: dayType,
                    WorkDate: r.WorkDate,
                    CheckInAt: r.CheckInAt,
                    CheckOutAt: r.CheckOutAt,
                    DayHours: r.DayHours,
                    NightHours: r.NightHours,
                    HasApprovedLateEarly: congContext.ExcusedDays.Contains((emp.Id, r.WorkDate)),
                    Shift: r.ShiftTemplate,
                    StandardHoursPerDay: congContext.StandardHoursPerDay,
                    LateGraceMinutes: congContext.LateGraceMinutes,
                    EarlyLeaveGraceMinutes: congContext.EarlyLeaveGraceMinutes));

                AccrueDayType(bucket, dayType, recognizedHours);

                // OnLeave nhưng ngày đó KHÔNG phải PaidLeaveDay ⇒ nghỉ không lương. Đếm riêng để FE
                // phân biệt "phép có lương" (đã vào PaidLeaveDays) vs "phép không lương".
                if (r.Status == AttendanceStatus.OnLeave && dayType != DayType.PaidLeaveDay)
                    bucket.UnpaidLeaveHours += recognizedHours;
            }

            bucket.DayHours += r.DayHours;
            bucket.NightHours += r.NightHours;

            // OT — subset của DayHours+NightHours, filter records gắn với OvertimeRequest.
            if (r.OvertimeRequestId is not null)
                bucket.OtHours += r.DayHours + r.NightHours;

            bucket.EquivalentHours += ComputeEquivalentHours(r, dayType, otDayDates, rateResolver, otCalculator, laborLawByDate);
        }

        decimal Cong(decimal hours) =>
            WorkDayCongCalculator.ToCong(hours, congContext.StandardHoursPerDay);

        return new EmployeeAttendanceSummaryDto(
            EmployeeInformationId: emp.Id,
            EmployeeFullName: emp.FullName,
            EmployeeCode: emp.User?.EmployeeCode,
            DepartmentId: emp.DepartmentId,
            DepartmentName: emp.Department?.Name,
            PositionName: emp.Position?.Name,
            PresentDays: bucket.PresentDays,
            AbsentDays: bucket.AbsentDays,
            LateDays: bucket.LateDays,
            HalfDays: bucket.HalfDays,
            OnLeaveDays: bucket.OnLeaveDays,
            EarlyLeaveDays: bucket.EarlyLeaveDays,
            TotalRecords: bucket.TotalRecords,
            NormalDays: Cong(bucket.NormalHours),
            WeeklyRestDays: Cong(bucket.WeeklyRestHoursForCong),
            HolidayDays: Cong(bucket.HolidayHoursForCong),
            PaidLeaveDays: Cong(bucket.PaidLeaveHours),
            UnpaidLeaveDays: Cong(bucket.UnpaidLeaveHours),
            DayHours: bucket.DayHours,
            NightHours: bucket.NightHours,
            TotalHours: bucket.DayHours + bucket.NightHours,
            EquivalentHours: bucket.EquivalentHours,
            OtHours: bucket.OtHours);
    }

    /// <summary>Cộng GIỜ công được công nhận vào đúng rổ loại-ngày; quy đổi sang công ở cuối.</summary>
    private static void AccrueDayType(Bucket bucket, DayType dayType, decimal recognizedHours)
    {
        switch (dayType)
        {
            case DayType.PublicHoliday:  bucket.HolidayHoursForCong += recognizedHours; break;
            case DayType.PaidLeaveDay:   bucket.PaidLeaveHours += recognizedHours; break;
            case DayType.WeeklyRestDay:  bucket.WeeklyRestHoursForCong += recognizedHours; break;
            default:                     bucket.NormalHours += recognizedHours; break;
        }
    }

    private static decimal ComputeEquivalentHours(
        AttendanceRecordEntity r,
        DayType dayType,
        HashSet<DateOnly> otDayDates,
        IAttendanceRateResolver rateResolver,
        IOvertimeMultiplierCalculator otCalculator,
        IReadOnlyDictionary<DateOnly, LaborLawConstantsSnapshot> laborLawByDate)
    {
        if (r.OvertimeRequestId is not null)
        {
            // OT: LaborLaw floor + uplift (Điều 98.1/98.3). Không lấy ShiftRate.
            if (!laborLawByDate.TryGetValue(r.WorkDate, out var law))
                return 0m; // defensive — prefetch bao phủ mọi record OT nên không xảy ra trong flow bình thường.

            var hasPrevDayOt = otDayDates.Contains(r.WorkDate);
            var dayOtRate   = otCalculator.Calculate(dayType, isNight: false, hasPrevDayOt: false, law);
            var nightOtRate = otCalculator.Calculate(dayType, isNight: true, hasPrevDayOt, law);
            return r.DayHours * dayOtRate / 100m + r.NightHours * nightOtRate / 100m;
        }

        // Non-OT: ShiftRate. Phụ cấp đêm cộng chỉ cho Normal day (T7/CN/Lễ/PaidLeave đã flat theo ShiftRate).
        var rate = rateResolver.Resolve(r.ShiftTemplate?.AppliedAttendanceRuleId, r.WorkDate, dayType);
        if (dayType == DayType.Normal)
        {
            var surcharge = laborLawByDate.TryGetValue(r.WorkDate, out var law) ? law.NightWorkSurchargePercent : 0m;
            return r.DayHours * rate / 100m + r.NightHours * (rate + surcharge) / 100m;
        }
        return (r.DayHours + r.NightHours) * rate / 100m;
    }

    private sealed class Bucket
    {
        public int PresentDays;
        public int AbsentDays;
        public int LateDays;
        public int HalfDays;
        public int OnLeaveDays;
        public int EarlyLeaveDays;
        public int TotalRecords;
        // Giờ công được công nhận, gom theo loại ngày — quy đổi sang công một lần ở cuối.
        public decimal NormalHours;
        public decimal WeeklyRestHoursForCong;
        public decimal HolidayHoursForCong;
        public decimal PaidLeaveHours;
        public decimal UnpaidLeaveHours;
        public decimal DayHours;
        public decimal NightHours;
        public decimal EquivalentHours;
        public decimal OtHours;
    }
}
