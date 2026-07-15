using MPS.Domain.Exceptions;
using LeaveTypeEntity = MPS.Domain.Entities.Attendance.LeaveType;

namespace MPS.Application.Features.Attendance.LeaveRequest.Common;

/// <summary>
/// Enforces "notice period" (SLA nội quy công ty): thời gian báo trước tối thiểu tùy độ dài đơn.
///  - totalDays ≤ 1  → MinNoticeDaysForShort
///  - totalDays ≤ 3  → MinNoticeDaysForMedium
///  - totalDays &gt; 5 → MinNoticeDaysForLong
/// Field null trong LeaveType = bỏ qua ngưỡng đó.
/// </summary>
internal static class LeaveNoticePeriodChecker
{
    public static void EnsureCompliant(
        LeaveTypeEntity leaveType,
        DateOnly startDate,
        DateOnly today,
        decimal totalDays)
    {
        var threshold = ResolveThreshold(leaveType, totalDays);
        if (threshold is null) return;

        var daysAhead = startDate.DayNumber - today.DayNumber;
        if (daysAhead >= threshold.Value) return;

        throw new DomainValidationException(
            $"Đơn nghỉ '{leaveType.Code}' {totalDays} ngày phải nộp trước ít nhất {threshold.Value} ngày. "
            + $"Hiện chỉ báo trước {daysAhead} ngày.");
    }

    private static int? ResolveThreshold(LeaveTypeEntity leaveType, decimal totalDays)
    {
        if (totalDays <= 1m) return leaveType.MinNoticeDaysForShort;
        if (totalDays <= 3m) return leaveType.MinNoticeDaysForMedium;
        if (totalDays > 5m) return leaveType.MinNoticeDaysForLong;
        // 3 < totalDays ≤ 5 → dùng ngưỡng Medium làm mặc định (chặt hơn).
        return leaveType.MinNoticeDaysForMedium;
    }
}
