using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Common;

/// <summary>
/// Tập trung kiểm tra giới hạn OT theo Điều 107 BLLĐ 2019.
/// Dùng chung cho cả CreateOvertimeRequest và ApproveOvertimeRequest (re-check).
/// Multi-shift: BR-5/6 cân nhắc TẤT CẢ ca trên ngày (tổng giờ chuẩn).
///
/// Cap tính cumulative gồm Pending + PendingApprover + Approved — chặn NV lạm dụng
/// gửi nhiều đơn Pending vượt cap trước khi HR duyệt. excludeRequestId để bỏ chính đơn
/// đang sửa/duyệt khỏi cumulative (tránh double-count).
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

    /// <summary>Statuses được tính là "đang chiếm cap" — chưa bị NV/HR huỷ hoặc reject.</summary>
    private static readonly IReadOnlyCollection<OtRequestStatus> ActiveStatuses = new[]
    {
        OtRequestStatus.Pending,
        OtRequestStatus.PendingApprover,
        OtRequestStatus.Approved,
    };

    public async Task CheckAsync(
        int employeeInformationId,
        DateOnly workDate,
        TimeOnly otStartTime,
        TimeOnly otEndTime,
        decimal otHours,
        CancellationToken cancellationToken,
        int? excludeRequestId = null)
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

        // Tổng giờ OT đang có (Pending + PendingApprover + Approved) của NV trên workDate,
        // exclude đơn đang sửa/duyệt để tránh double-count.
        var existingDayOtHours = await _otRepository.GetTotalOtHoursOnDateAsync(
            employeeInformationId, workDate, ActiveStatuses, excludeRequestId, cancellationToken);
        var totalOtHoursAfter = existingDayOtHours + otHours;

        if (assignments.Count > 0)
        {
            // BR-6: tổng giờ chuẩn các ca + OT ≤ dailyTotalCap; OT ≤ 50% tổng giờ ca.
            var totalShiftHours = assignments.Sum(a => a.ShiftTemplate.StandardHours);
            var halfShift = totalShiftHours / 2m;
            if (totalOtHoursAfter > halfShift)
                throw new DomainValidationException(
                    $"Ca chính hôm nay {FormatHours(totalShiftHours)} → tối đa OT {FormatHours(halfShift)} theo quy định 50% thời lượng ca. Bạn đã có {FormatHours(existingDayOtHours)} OT đang chờ/đã duyệt, đăng ký thêm {FormatHours(otHours)} sẽ thành {FormatHours(totalOtHoursAfter)}.");
            if (totalShiftHours + totalOtHoursAfter > dailyTotalCap)
                throw new DomainValidationException(
                    $"Tổng thời gian làm việc trong ngày ({FormatHours(totalShiftHours)} ca chính + {FormatHours(totalOtHoursAfter)} OT) vượt mức cho phép {FormatHours(dailyTotalCap)}/ngày.");
        }
        else
        {
            // Chưa phân ca: dùng OvertimeMaxHoursPerDay từ luật.
            if (totalOtHoursAfter > laborLaw.OvertimeMaxHoursPerDay)
                throw new DomainValidationException(
                    $"Vượt giới hạn OT trong ngày. Bạn đã có {FormatHours(existingDayOtHours)} OT đang chờ/đã duyệt, đăng ký thêm {FormatHours(otHours)} sẽ thành {FormatHours(totalOtHoursAfter)}, tối đa {FormatHours(laborLaw.OvertimeMaxHoursPerDay)}/ngày theo luật.");
        }

        // BR-7: monthly cap từ luật (Điều 107.2.b — 40h/tháng default, có thể tăng theo NĐ145/2020).
        // Tính cả Pending + PendingApprover — nếu không, NV có thể spam Pending vượt cap.
        var monthlyExisting = await _otRepository.GetTotalOtHoursInMonthAsync(
            employeeInformationId, workDate.Year, workDate.Month, ActiveStatuses, cancellationToken);
        var monthlyExistingExcluded = await SubtractExcludedAsync(
            monthlyExisting, excludeRequestId, cancellationToken);
        if (monthlyExistingExcluded + otHours > laborLaw.OvertimeMaxHoursPerMonth)
        {
            var monthlyRemaining = laborLaw.OvertimeMaxHoursPerMonth - monthlyExistingExcluded;
            throw new DomainValidationException(
                $"Vượt giới hạn OT tháng {workDate.Month}/{workDate.Year}. Đã có {FormatHours(monthlyExistingExcluded)} (gồm Pending + đã duyệt), còn được đăng ký {FormatHours(monthlyRemaining < 0 ? 0 : monthlyRemaining)} (giới hạn {FormatHours(laborLaw.OvertimeMaxHoursPerMonth)}/tháng). Bạn đăng ký {FormatHours(otHours)}.");
        }

        // BR-8: yearly cap
        var yearlyExisting = await _otRepository.GetTotalOtHoursInYearAsync(
            employeeInformationId, workDate.Year, ActiveStatuses, cancellationToken);
        var yearlyExistingExcluded = await SubtractExcludedAsync(
            yearlyExisting, excludeRequestId, cancellationToken);
        if (yearlyExistingExcluded + otHours > laborLaw.OvertimeMaxHoursPerYear)
        {
            var yearlyRemaining = laborLaw.OvertimeMaxHoursPerYear - yearlyExistingExcluded;
            throw new DomainValidationException(
                $"Vượt giới hạn OT năm {workDate.Year}. Đã có {FormatHours(yearlyExistingExcluded)} (gồm Pending + đã duyệt), còn được đăng ký {FormatHours(yearlyRemaining < 0 ? 0 : yearlyRemaining)} (giới hạn {FormatHours(laborLaw.OvertimeMaxHoursPerYear)}/năm). Bạn đăng ký {FormatHours(otHours)}.");
        }
    }

    /// <summary>
    /// Trừ giờ OT của 1 request khỏi tổng (để tránh double-count khi đang sửa/duyệt chính nó).
    /// Repository monthly/yearly sum không có tham số exclude nên trừ thủ công ở đây.
    /// </summary>
    private async Task<decimal> SubtractExcludedAsync(
        decimal total, int? excludeRequestId, CancellationToken cancellationToken)
    {
        if (excludeRequestId is null) return total;
        var excluded = await _otRepository.GetByIdAsync(excludeRequestId.Value, cancellationToken);
        if (excluded is null) return total;
        if (!ActiveStatuses.Contains(excluded.Status)) return total;
        var adjusted = total - excluded.OtHours;
        return adjusted < 0 ? 0 : adjusted;
    }

    /// <summary>
    /// Format số giờ cho message user-facing:
    /// - &lt;1h → "X phút" (dễ đọc hơn "0.50 giờ")
    /// - còn lại → "X.XX giờ" (2 chữ số thập phân)
    /// </summary>
    private static string FormatHours(decimal hours)
    {
        if (hours <= 0) return "0 giờ";
        if (hours < 1m)
        {
            var minutes = Math.Round(hours * 60m, MidpointRounding.AwayFromZero);
            return $"{minutes:0} phút";
        }
        return $"{hours:0.##} giờ";
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
