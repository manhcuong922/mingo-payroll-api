namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Tách giờ làm thực tế thành (DayHours, NightHours) dựa trên khung giờ đêm
/// (mặc định 22h–6h theo Điều 106 BLLĐ).
///
/// Là pure function — không I/O, không state. Test bằng xUnit với ma trận đầy đủ:
/// no-cross-midnight, cross-midnight, break trùng / không trùng night window,
/// edge case CheckOut == NightStart, ca toàn đêm / toàn ngày.
/// </summary>
public interface IWorkHourSplitter
{
    WorkHourBreakdown Split(WorkHourSplitInput input);
}

/// <summary>
/// Input cho splitter — đầy đủ thông tin để compute mà không cần lookup thêm.
/// </summary>
public sealed record WorkHourSplitInput(
    TimeOnly CheckIn,
    TimeOnly CheckOut,
    TimeOnly? BreakStart,
    TimeOnly? BreakEnd,
    TimeOnly NightStart,
    TimeOnly NightEnd);

public sealed record WorkHourBreakdown(decimal DayHours, decimal NightHours)
{
    public decimal Total => DayHours + NightHours;

    public static readonly WorkHourBreakdown Zero = new(0m, 0m);
}
