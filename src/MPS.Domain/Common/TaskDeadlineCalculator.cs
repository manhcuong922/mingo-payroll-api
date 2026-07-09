namespace MPS.Domain.Common;

/// <summary>
/// Công thức 2 chiều deadline ↔ ratio dùng chung giữa Generator, Recompute và Task Create/Update
/// (refactor task-full-generation, mục thời gian):
///   task.Deadline = start + ratio × (dl − start)   ⇔   ratio = (task.Deadline − start) / (dl − start)
/// Đơn không Deadline hoặc timeline không hợp lệ (span ≤ 0) ⇒ trả null: task giữ chế độ manual/sticky,
/// không tham gia cascade.
/// </summary>
public static class TaskDeadlineCalculator
{
    // Ratio (0..1) → DateTime tuyệt đối. Dùng khi generate/recompute deadline task từ ratio đã có.
    public static DateTime? DeadlineFromRatio(DateOnly startDate, DateTime? projectDeadline, double? ratio)
    {
        if (ratio is not { } r || projectDeadline is not { } deadline) return null;

        var start = startDate.ToDateTime(TimeOnly.MinValue);
        var span = deadline - start;
        if (span <= TimeSpan.Zero) return null;

        return start.AddTicks((long)(span.Ticks * Math.Clamp(r, 0, 1)));
    }

    // Deadline tuyệt đối → ratio 0..1. Dùng khi tạo/sửa task để "khóa" vị trí tương đối, sao cho lần
    // đổi timeline đơn kế tiếp vẫn đúng ý người dùng (không bị recompute wipe).
    public static double? RatioFromDeadline(DateOnly startDate, DateTime? projectDeadline, DateTime? taskDeadline)
    {
        if (taskDeadline is not { } td || projectDeadline is not { } deadline) return null;

        var start = startDate.ToDateTime(TimeOnly.MinValue);
        var span = deadline - start;
        if (span <= TimeSpan.Zero) return null;

        var offset = (td - start).TotalSeconds / span.TotalSeconds;
        return Math.Clamp(offset, 0, 1);
    }
}
