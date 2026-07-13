using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using AttendanceRecordEntity = MPS.Domain.Entities.Attendance.AttendanceRecord;
using AttendanceRuleEntity = MPS.Domain.Entities.Attendance.AttendanceRule;
using EmployeeInformationEntity = MPS.Domain.Entities.EmployeeInformation;

namespace MPS.Application.Features.Attendance.Attendance.Queries;

/// <summary>
/// Bảng chấm công tháng — aggregate theo NV × DayType × giờ × hệ số.
/// - Status-based counts (Present/Absent/Late/HalfDay/OnLeave/EarlyLeave/Total) — cho Báo cáo cũ.
/// - DayType-based aggregations (Normal/WeeklyRest/Holiday/PaidLeave) — cho Bảng chấm công tháng.
///   Halfday cộng 0.5 vào DayType tương ứng.
/// - Giờ (DayHours/NightHours/Total) + quy đổi × hệ số.
///
/// Perf: paginate ở tầng NV (không phải record). DayType resolve per-NV (max size query),
/// records batch 1 query cho toàn page.
/// </summary>
public class GetAttendanceSummaryQueryHandler
    : IRequestHandler<GetAttendanceSummaryQuery, PagedResult<EmployeeAttendanceSummaryDto>>
{
    private const decimal NightRateBonus = 1.30m;
    private const decimal HalfDayWeight = 0.5m;
    private const decimal FullDayWeight = 1.0m;
    private const int MaxPageSize = 1000;
    private const int DefaultPageSize = 20;

    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IAttendanceRecordRepository _attendanceRepository;
    private readonly IDayTypeResolver _dayTypeResolver;

    public GetAttendanceSummaryQueryHandler(
        IEmployeeInformationRepository employeeRepository,
        IAttendanceRecordRepository attendanceRepository,
        IDayTypeResolver dayTypeResolver)
    {
        _employeeRepository = employeeRepository;
        _attendanceRepository = attendanceRepository;
        _dayTypeResolver = dayTypeResolver;
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

        // Tầng 3 — mỗi NV: resolve DayType range (prefetch, no N+1 trong resolver) + aggregate
        var results = new List<EmployeeAttendanceSummaryDto>(employees.Count);
        foreach (var emp in employees)
        {
            var dayMap = await _dayTypeResolver.ResolveRangeAsync(
                emp.Id, request.FromDate, request.ToDate, cancellationToken);

            var empRecords = recordsByEmp.TryGetValue(emp.Id, out var list)
                ? list
                : Array.Empty<AttendanceRecordEntity>();

            results.Add(Aggregate(emp, empRecords, dayMap));
        }

        return new PagedResult<EmployeeAttendanceSummaryDto>(results, total, page, size);
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
        IReadOnlyDictionary<DateOnly, DayType> dayMap)
    {
        var bucket = new Bucket();

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

            // Giờ + quy đổi × hệ số
            bucket.DayHours += r.DayHours;
            bucket.NightHours += r.NightHours;

            // OT — subset của DayHours+NightHours, filter records gắn với OvertimeRequest.
            if (r.OvertimeRequestId is not null)
                bucket.OtHours += r.DayHours + r.NightHours;

            var rule = r.ShiftTemplate?.AppliedAttendanceRule;
            var rate = ResolveRate(rule, dayType);
            bucket.EquivalentHours += r.DayHours * rate + r.NightHours * rate * NightRateBonus;
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

    private static decimal ResolveRate(AttendanceRuleEntity? rule, DayType dayType)
    {
        if (rule is null) return 1.0m;
        return dayType switch
        {
            DayType.PublicHoliday => rule.WorkRateHoliday,
            DayType.WeeklyRestDay => rule.WorkRateWeeklyRest,
            _ => rule.WorkRateNormal,
        };
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
