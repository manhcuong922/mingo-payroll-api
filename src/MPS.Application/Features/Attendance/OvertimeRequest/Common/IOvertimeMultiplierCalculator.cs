using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Common;

/// <summary>
/// Tính hệ số OT (%) cho 1 segment theo công thức Điều 98 BLLĐ 2019 + Thông tư 18/2021.
///
/// Mọi số nguyên tố lấy từ <see cref="LaborLawConstantsSnapshot"/> — không hard-code:
///  - <c>WorkRateNormalFloor</c> / <c>WorkRateWeeklyRestFloor</c> / <c>WorkRateHolidayFloor</c> (Đ.98.1)
///  - <c>OvertimeNormalUpliftPercent</c> (Đ.98.1.a — +50% cho OT ngày thường)
///  - <c>NightWorkSurchargePercent</c> (Đ.98.2 — +30% làm ca đêm)
///  - <c>OvertimeNightExtraPercent</c> (Đ.98.3 — +20% × base cho OT đêm)
///
/// Composer load <c>LaborLawConstantsSnapshot</c> 1 lần từ <c>OvertimeRequest.LaborLawConstantsVersionId</c>
/// đã snapshot, gọi <see cref="Calculate"/> cho từng segment. Nhờ vậy rate không đổi khi HR sửa luật
/// sau khi request tạo/duyệt.
/// </summary>
public interface IOvertimeMultiplierCalculator
{
    /// <summary>
    /// Pure formula. dayType phân biệt Normal/WeeklyRest/PublicHoliday/PaidLeaveDay.
    /// isNight = segment nằm trong khung <c>[NightStartTime, NightEndTime]</c> của laborLaw.
    /// hasPrevDayOt = trong cùng WorkDate và trước segment này đã có OT ban ngày (Approved/Pending).
    /// </summary>
    decimal Calculate(DayType dayType, bool isNight, bool hasPrevDayOt, LaborLawConstantsSnapshot laborLaw);

    /// <summary>
    /// Convenience overload dùng LaborLaw active tại workDate. Dùng cho preview/UI khi
    /// composer chưa snapshot. Composer thực tế nên dùng overload sync với snapshot đã khoá.
    /// </summary>
    Task<decimal> CalculateAtAsync(
        DayType dayType,
        bool isNight,
        bool hasPrevDayOt,
        DateOnly workDate,
        CancellationToken cancellationToken);
}
