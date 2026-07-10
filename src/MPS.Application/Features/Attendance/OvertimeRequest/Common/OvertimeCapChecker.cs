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
                    $"Thời gian làm thêm bị trùng với ca làm việc chính '{a.ShiftTemplate.Code}' vào ngày {a.WorkDate:yyyy-MM-dd} (từ {a.ShiftTemplate.StartTime:HH\\:mm} đến {a.ShiftTemplate.EndTime:HH\\:mm}).");
            }
        }

        // Daily total cap = standard giờ chuẩn + OT max (Điều 105 + Điều 107.2.b BLLĐ),
        // đọc từ LaborLawConstants active tại workDate — không hard-code.
        var dailyTotalCap = laborLaw.StandardWorkingHoursPerDay + laborLaw.OvertimeMaxHoursPerDay;

        if (assignments.Count > 0)
        {
            // BR-6: tổng giờ chuẩn các ca + OT ≤ dailyTotalCap; OT ≤ 50% tổng giờ ca.
            var totalShiftHours = assignments.Sum(a => a.ShiftTemplate.StandardHours);
            var halfShift = totalShiftHours / 2m;
            if (otHours > halfShift)
                throw new DomainValidationException(
                    $"Thời gian làm thêm không được vượt quá 50% thời lượng ca chính (tối đa {halfShift} giờ).");
            if (totalShiftHours + otHours > dailyTotalCap)
                throw new DomainValidationException(
                    $"Tổng thời gian làm việc (gồm {totalShiftHours} giờ ca chính và {otHours} giờ làm thêm) đã vượt mức cho phép {dailyTotalCap} giờ/ngày.");
        }
        else
        {
            // Chưa phân ca: dùng OvertimeMaxHoursPerDay từ luật.
            if (otHours > laborLaw.OvertimeMaxHoursPerDay)
                throw new DomainValidationException(
                    $"Thời gian làm thêm trong ngày vượt mức giới hạn (yêu cầu {otHours} giờ, nhưng tối đa chỉ được {laborLaw.OvertimeMaxHoursPerDay} giờ).");
        }

        // BR-7: monthly cap từ luật (Điều 107.2.b — 40h/tháng default, có thể tăng theo NĐ145/2020)
        var monthlyApproved = await _otRepository.GetTotalOtHoursInMonthAsync(
            employeeInformationId, workDate.Year, workDate.Month, cancellationToken);
        if (monthlyApproved + otHours > laborLaw.OvertimeMaxHoursPerMonth)
            throw new DomainValidationException(
                $"Đã vượt giới hạn giờ làm thêm trong tháng. (Đã duyệt: {monthlyApproved} giờ, yêu cầu mới: {otHours} giờ, giới hạn tối đa: {laborLaw.OvertimeMaxHoursPerMonth} giờ).");

        // BR-8: yearly cap
        var yearlyApproved = await _otRepository.GetTotalOtHoursInYearAsync(
            employeeInformationId, workDate.Year, cancellationToken);
        if (yearlyApproved + otHours > laborLaw.OvertimeMaxHoursPerYear)
            throw new DomainValidationException(
                $"Đã vượt giới hạn giờ làm thêm trong năm. (Đã duyệt: {yearlyApproved} giờ, yêu cầu mới: {otHours} giờ, giới hạn tối đa: {laborLaw.OvertimeMaxHoursPerYear} giờ).");
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
