using MPS.Domain.Exceptions;

namespace MPS.Application.Features.Attendance.Common;

/// <summary>
/// Assertion dùng chung cho các loại đơn (Leave/WFH/…) yêu cầu KHOẢNG ngày đơn phải nằm
/// HOÀN TOÀN trong lịch phân ca của NV. Chặn kịch bản NV chọn range vượt ra ngoài phân ca
/// (VD phân ca đến hết tháng 8, đơn xin tới tháng 10) khiến các ngày rỗng bị hiểu nhầm là
/// "đang nghỉ / WFH / OT" khi HR phân ca bù về sau.
/// </summary>
public static class ShiftCoverageAssertion
{
    private const int PreviewLimit = 5;

    /// <summary>
    /// Throw <see cref="DomainValidationException"/> nếu có ngày <b>làm việc</b> nào trong
    /// [<paramref name="start"/>, <paramref name="end"/>] chưa được phân ca. Ngày nghỉ tuần
    /// (T7/CN theo pattern) và ngày lễ liệt kê trong <paramref name="skippableDays"/> sẽ được
    /// bỏ qua — NV vẫn xin nghỉ được cả tuần (Mon→Sun) mà không cần phân ca cho T7/CN.
    /// Thông báo liệt kê đến <see cref="PreviewLimit"/> ngày đầu tiên bị thiếu.
    /// </summary>
    /// <param name="requestLabel">Nhãn hiển thị trong lỗi (VD "nghỉ 'SICK'", "WFH", "OT"...).</param>
    /// <param name="skippableDays">Tập ngày không cần đòi phân ca — thường là WeeklyRestDay + PublicHoliday.</param>
    public static void EnsureRangeFullyCovered(
        string requestLabel,
        DateOnly start,
        DateOnly end,
        IReadOnlyCollection<DateOnly> assignedDays,
        IReadOnlyCollection<DateOnly>? skippableDays = null)
    {
        var assignedSet = assignedDays as HashSet<DateOnly> ?? assignedDays.ToHashSet();
        var skipSet = skippableDays as HashSet<DateOnly> ?? skippableDays?.ToHashSet();

        var missing = new List<DateOnly>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (assignedSet.Contains(d)) continue;
            if (skipSet is not null && skipSet.Contains(d)) continue;
            missing.Add(d);
        }
        if (missing.Count == 0) return;

        var preview = string.Join(", ", missing.Take(PreviewLimit).Select(d => d.ToString("dd/MM/yyyy")));
        var suffix = missing.Count > PreviewLimit
            ? $" (và {missing.Count - PreviewLimit} ngày khác)"
            : string.Empty;
        throw new DomainValidationException(
            $"Đơn {requestLabel} chỉ được gửi trong khoảng đã phân ca. "
            + $"Ngày chưa có ca: {preview}{suffix}. Vui lòng thu hẹp khoảng hoặc đề nghị HR phân ca trước.");
    }
}
