using MediatR;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

/// <summary>
/// NV xem tổng giờ OT hôm nay + tháng hiện tại — tách Approved (đã duyệt) vs Pending
/// (chờ tổ trưởng review + chờ HR/DM duyệt cuối). Kèm cap ngày/tháng từ LaborLaw.
/// Dùng ở section ngang trên màn "Đơn của tôi" → tab Tăng ca.
/// </summary>
public sealed record GetMyOvertimeYearlySummaryQuery : IRequest<MyOvertimeYearlySummaryDto>;

public sealed record MyOvertimeYearlySummaryDto(
    /// <summary>Ngày tính giờ hôm nay (server time zone).</summary>
    DateOnly Today,
    /// <summary>Giờ OT Approved hôm nay.</summary>
    decimal TodayApprovedHours,
    /// <summary>Giờ OT Pending + PendingApprover hôm nay.</summary>
    decimal TodayPendingHours,
    /// <summary>Cap giờ OT/ngày theo LaborLaw active (VD 4h theo luật; ca cụ thể có thể chặn thấp hơn).</summary>
    decimal DailyCap,
    /// <summary>Giờ OT Approved trong tháng hiện tại.</summary>
    decimal MonthApprovedHours,
    /// <summary>Giờ OT Pending + PendingApprover trong tháng hiện tại.</summary>
    decimal MonthPendingHours,
    /// <summary>Cap tháng theo LaborLaw active (VD 40).</summary>
    decimal MonthlyCap);
