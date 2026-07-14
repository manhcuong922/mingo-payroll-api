using MediatR;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

/// <summary>
/// Query trả về các giới hạn OT hiện hành (active LaborLawConstants) cho NV xem trước
/// khi đăng ký. Không cần permission compliance vì chỉ expose 4 con số công khai
/// (không có field liên quan lương, BHXH). Dùng ở màn "Đơn của tôi" → tab Tăng ca.
/// </summary>
public sealed record GetOvertimeLimitsQuery : IRequest<OvertimeLimitsDto>;

public sealed record OvertimeLimitsDto(
    /// <summary>Giờ chuẩn ca chính/ngày (VD 8h).</summary>
    decimal StandardWorkingHoursPerDay,
    /// <summary>Giờ OT tối đa/ngày (VD 4h) — dùng khi NV chưa có ca chính assigned.</summary>
    decimal OvertimeMaxHoursPerDay,
    /// <summary>Giờ OT tối đa/tháng (VD 40h).</summary>
    decimal OvertimeMaxHoursPerMonth,
    /// <summary>Giờ OT tối đa/năm (VD 300h).</summary>
    int OvertimeMaxHoursPerYear,
    /// <summary>Cap tổng ca + OT/ngày = StandardWorkingHoursPerDay + OvertimeMaxHoursPerDay (VD 12h).</summary>
    decimal DailyTotalCap,
    /// <summary>Reference luật (VD "BLLĐ 2019 Điều 26, 98, 107, 113 + NĐ 145/2020 Điều 54").</summary>
    string LegalReference);
