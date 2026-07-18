namespace MPS.Domain.Enums.Attendance;

/// <summary>
/// Loại đơn đi trễ / về sớm.
/// </summary>
public enum LateEarlyKind
{
    /// <summary>Đi trễ giờ vào ca.</summary>
    LateArrival = 1,
    /// <summary>Về sớm trước giờ tan ca.</summary>
    EarlyLeave = 2
}
