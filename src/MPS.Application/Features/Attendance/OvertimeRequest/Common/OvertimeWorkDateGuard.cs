using MPS.Domain.Exceptions;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Common;

/// <summary>
/// Case 4 — chặn đăng ký/sửa OT cho ngày quá xa trong TƯƠNG LAI. OT là làm thêm giờ thực tế nên chỉ
/// đăng ký khi sắp/đang làm, không đặt trước hàng tháng. QUÁ KHỨ vẫn cho phép — cơ chế truy lĩnh
/// <c>[D3]</c> (OT duyệt sau khi kỳ đã chốt) cố ý hỗ trợ OT hồi tố.
///
/// Ngưỡng đồng bộ với đơn đi muộn/về sớm (±30 ngày). Thuần để test độc lập.
/// </summary>
public static class OvertimeWorkDateGuard
{
    public const int MaxFutureDays = 30;

    // WorkDate là ngày theo LỊCH VIỆT NAM (UTC+7, không DST). So mốc theo ngày VN, không theo ngày UTC —
    // tránh lệch 1 ngày lúc chiều/tối UTC (khi VN đã sang ngày mới).
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static void EnsureNotTooFarInFuture(DateOnly workDate, DateTime nowUtc)
    {
        var todayVn = DateOnly.FromDateTime(nowUtc + VietnamOffset);
        var maxDate = todayVn.AddDays(MaxFutureDays);
        if (workDate > maxDate)
            throw new DomainValidationException(
                $"OT_WORKDATE_TOO_FAR: Không thể đăng ký OT cho ngày quá {MaxFutureDays} ngày trong tương lai. " +
                "OT chỉ đăng ký khi sắp hoặc đang làm thêm.");
    }
}
