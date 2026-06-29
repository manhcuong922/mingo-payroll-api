namespace MPS.Application.Features.Attendance.Common;

/// <summary>
/// Caps cho giờ làm việc bình thường — Điều 105 BLLĐ.
/// Tách biệt với cap OT (Điều 107 — xem <c>OvertimeCapChecker</c>).
/// </summary>
/// <remarks>
/// TODO §10: chuyển sang đọc từ <c>LaborLawConstantsVersion</c> qua
/// <c>IComplianceLookupService</c> khi Phase 1c (WorkingTimeMode) land.
/// 10h/ngày của mode Weekly (Điều 105.2) chưa hỗ trợ trong Phase 1a/1b.
/// </remarks>
public static class RegularHoursCap
{
    /// <summary>Điều 105.1: ≤ 8h/ngày khi áp mode Daily.</summary>
    public const decimal MaxPerDay = 8m;

    /// <summary>Điều 105.1+2: ≤ 48h/tuần áp dụng cho cả Daily và Weekly mode.</summary>
    public const decimal MaxPerWeek = 48m;
}
