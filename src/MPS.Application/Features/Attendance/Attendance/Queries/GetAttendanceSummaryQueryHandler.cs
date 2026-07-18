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
///   Halfday cộng 0.5 vào DayType tương ứng.
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
    private const decimal HalfDayWeight = 0.5m;
    private const decimal FullDayWeight = 1.0m;
    private const int MaxPageSize = 1000;
    private const int DefaultPageSize = 20;

    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IAttendanceRecordRepository _attendanceRepository;
    private readonly IDayTypeResolver _dayTypeResolver;
    private readonly IAttendanceRateResolver _rateResolver;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly IOvertimeMultiplierCalculator _otCalculator;

    public GetAttendanceSummaryQueryHandler(
        IEmployeeInformationRepository employeeRepository,
        IAttendanceRecordRepository attendanceRepository,
        IDayTypeResolver dayTypeResolver,
        IAttendanceRateResolver rateResolver,
        IComplianceLookupService complianceLookup,
        IOvertimeMultiplierCalculator otCalculator)
    {
        _employeeRepository = employeeRepository;
        _attendanceRepository = attendanceRepository;
        _dayTypeResolver = dayTypeResolver;
        _rateResolver = rateResolver;
        _complianceLookup = complianceLookup;
        _otCalculator = otCalculator;
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

        // Tầng 3 — mỗi NV: resolve DayType range (prefetch, no N+1 trong resolver) + aggregate
        var results = new List<EmployeeAttendanceSummaryDto>(employees.Count);
        foreach (var emp in employees)
        {
            var dayMap = await _dayTypeResolver.ResolveRangeAsync(
                emp.Id, request.FromDate, request.ToDate, cancellationToken);

            var empRecords = recordsByEmp.TryGetValue(emp.Id, out var list)
                ? list
                : Array.Empty<AttendanceRecordEntity>();

            results.Add(Aggregate(emp, empRecords, dayMap, _rateResolver, _otCalculator, laborLawByDate));
        }

        return new PagedResult<EmployeeAttendanceSummaryDto>(results, total, page, size);
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
        IReadOnlyDictionary<DateOnly, LaborLawConstantsSnapshot> laborLawByDate)
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

            var weight = r.Status == AttendanceStatus.HalfDay ? HalfDayWeight : FullDayWeight;
            var dayType = dayMap.TryGetValue(r.WorkDate, out var dt) ? dt : DayType.Normal;
            AccrueDayType(bucket, dayType, weight);

            bucket.DayHours += r.DayHours;
            bucket.NightHours += r.NightHours;

            // OT — subset của DayHours+NightHours, filter records gắn với OvertimeRequest.
            if (r.OvertimeRequestId is not null)
                bucket.OtHours += r.DayHours + r.NightHours;

            bucket.EquivalentHours += ComputeEquivalentHours(r, dayType, otDayDates, rateResolver, otCalculator, laborLawByDate);
        }

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
            NormalDays: bucket.NormalDays,
            WeeklyRestDays: bucket.WeeklyRestDays,
            HolidayDays: bucket.HolidayDays,
            PaidLeaveDays: bucket.PaidLeaveDays,
            DayHours: bucket.DayHours,
            NightHours: bucket.NightHours,
            TotalHours: bucket.DayHours + bucket.NightHours,
            EquivalentHours: bucket.EquivalentHours,
            OtHours: bucket.OtHours);
    }

    private static void AccrueDayType(Bucket bucket, DayType dayType, decimal weight)
    {
        switch (dayType)
        {
            case DayType.PublicHoliday:  bucket.HolidayDays += weight; break;
            case DayType.PaidLeaveDay:   bucket.PaidLeaveDays += weight; break;
            case DayType.WeeklyRestDay:  bucket.WeeklyRestDays += weight; break;
            default:                     bucket.NormalDays += weight; break;
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
        public decimal NormalDays;
        public decimal WeeklyRestDays;
        public decimal HolidayDays;
        public decimal PaidLeaveDays;
        public decimal DayHours;
        public decimal NightHours;
        public decimal EquivalentHours;
        public decimal OtHours;
    }
}
