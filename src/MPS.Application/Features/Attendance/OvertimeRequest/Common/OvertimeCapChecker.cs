using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Common;

/// <summary>
/// Tập trung kiểm tra giới hạn OT theo Điều 107 BLLĐ 2019.
/// Dùng chung cho cả CreateOvertimeRequest và ApproveOvertimeRequest (re-check).
/// Multi-shift: BR-5/6 cân nhắc TẤT CẢ ca trên ngày (tổng giờ chuẩn).
/// </summary>
public sealed class OvertimeCapChecker
{
    public const decimal MonthlyOvertimeCapHours = 40m;
    public const decimal DailyTotalWorkCapHours = 12m;

    private readonly IOvertimeRequestRepository _otRepository;
    private readonly IEmployeeShiftAssignmentRepository _shiftRepository;
    private readonly IComplianceLookupService _complianceLookup;

    public OvertimeCapChecker(
        IOvertimeRequestRepository otRepository,
        IEmployeeShiftAssignmentRepository shiftRepository,
        IComplianceLookupService complianceLookup)
    {
        _otRepository = otRepository;
        _shiftRepository = shiftRepository;
        _complianceLookup = complianceLookup;
    }

    public async Task CheckAsync(
        int employeeInformationId,
        DateOnly workDate,
        TimeOnly otStartTime,
        TimeOnly otEndTime,
        decimal otHours,
        CancellationToken cancellationToken)
    {
        var laborLaw = await _complianceLookup.GetActiveLaborLawConstantsAsync(cancellationToken);

        // BR-5 needs nearby dates because either the OT or the regular shift can cross midnight.
        var overlapAssignments = await _shiftRepository.GetByEmployeesAndDateRangeAsync(
            new[] { employeeInformationId },
            workDate.AddDays(-1),
            workDate.AddDays(1),
            cancellationToken: cancellationToken);

        // BR-6 still uses the shifts assigned to the OT business date.
        var assignments = overlapAssignments
            .Where(a => a.WorkDate == workDate)
            .ToList();

        foreach (var a in overlapAssignments)
        {
            if (Overlaps(
                    workDate, otStartTime, otEndTime,
                    a.WorkDate, a.ShiftTemplate.StartTime, a.ShiftTemplate.EndTime))
            {
                throw new DomainValidationException(
                    $"BR-5: OT overlaps regular shift '{a.ShiftTemplate.Code}' on {a.WorkDate:yyyy-MM-dd} ({a.ShiftTemplate.StartTime:HH\\:mm}–{a.ShiftTemplate.EndTime:HH\\:mm}).");
            }
        }

        if (assignments.Count > 0)
        {
            // BR-6: tổng giờ chuẩn các ca + OT ≤ 12h/ngày; OT ≤ 50% tổng giờ ca.
            var totalShiftHours = assignments.Sum(a => a.ShiftTemplate.StandardHours);
            var halfShift = totalShiftHours / 2m;
            if (otHours > halfShift)
                throw new DomainValidationException(
                    $"BR-6: Daily OT cannot exceed 50% of total shift hours ({halfShift}h).");
            if (totalShiftHours + otHours > DailyTotalWorkCapHours)
                throw new DomainValidationException(
                    $"BR-6: Total work (shifts {totalShiftHours}h + OT {otHours}h) exceeds {DailyTotalWorkCapHours}h/day.");
        }
        else
        {
            // Chưa phân ca: dùng OvertimeMaxHoursPerDay từ luật.
            if (otHours > laborLaw.OvertimeMaxHoursPerDay)
                throw new DomainValidationException(
                    $"BR-6: Daily OT cap exceeded ({otHours}h > {laborLaw.OvertimeMaxHoursPerDay}h).");
        }

        // BR-7: monthly cap
        var monthlyApproved = await _otRepository.GetTotalOtHoursInMonthAsync(
            employeeInformationId, workDate.Year, workDate.Month, cancellationToken);
        if (monthlyApproved + otHours > MonthlyOvertimeCapHours)
            throw new DomainValidationException(
                $"BR-7: Monthly OT cap exceeded. Approved={monthlyApproved}h, requested={otHours}h, cap={MonthlyOvertimeCapHours}h.");

        // BR-8: yearly cap
        var yearlyApproved = await _otRepository.GetTotalOtHoursInYearAsync(
            employeeInformationId, workDate.Year, cancellationToken);
        if (yearlyApproved + otHours > laborLaw.OvertimeMaxHoursPerYear)
            throw new DomainValidationException(
                $"BR-8: Yearly OT cap exceeded. Approved={yearlyApproved}h, requested={otHours}h, cap={laborLaw.OvertimeMaxHoursPerYear}h.");
    }

    private static bool Overlaps(
        DateOnly aDate,
        TimeOnly aStart,
        TimeOnly aEnd,
        DateOnly bDate,
        TimeOnly bStart,
        TimeOnly bEnd)
    {
        var (aAbsoluteStart, aAbsoluteEnd) = ToAbsoluteRange(aDate, aStart, aEnd);
        var (bAbsoluteStart, bAbsoluteEnd) = ToAbsoluteRange(bDate, bStart, bEnd);

        return aAbsoluteStart < bAbsoluteEnd && bAbsoluteStart < aAbsoluteEnd;
    }

    private static (long Start, long End) ToAbsoluteRange(
        DateOnly date,
        TimeOnly start,
        TimeOnly end)
    {
        var baseTicks = (long)date.DayNumber * TimeSpan.TicksPerDay;
        var absoluteStart = baseTicks + start.Ticks;
        var absoluteEnd = baseTicks + end.Ticks;

        if (absoluteEnd <= absoluteStart)
        {
            absoluteEnd += TimeSpan.TicksPerDay;
        }

        return (absoluteStart, absoluteEnd);
    }
}
