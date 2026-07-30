namespace MPS.Domain.Entities.Attendance;

public class AttendanceSetting
{
    public int Id { get; set; }

    public bool IsEditLockEnabled { get; set; }
    public TimeOnly DailyEditCutoffTime { get; set; }
    public int LockAfterDays { get; set; }
    public bool AllowAdminOverride { get; set; }
    public bool RequireOverrideReason { get; set; }

    // ===== Company policy config (nullable = fallback to hardcoded default in checker) =====

    /// <summary>Số lần đi trễ/về sớm tối đa trong tháng trước khi bị trừ quỹ phép. Null = 2.</summary>
    public int? LateEarlyMaxOccurrencesPerMonth { get; set; }

    /// <summary>Số phút tối đa cho mỗi lần đi trễ/về sớm. Null = 30.</summary>
    public int? LateEarlyMaxMinutesPerOccurrence { get; set; }

    /// <summary>Số ngày tối đa cho phép nộp đơn Late/Early sau WorkDate (đột xuất). Null = 2.</summary>
    public int? LateEarlyBackdateDaysAllowed { get; set; }

    /// <summary>Quỹ WFH tháng, đơn vị ngày. Null = 4.</summary>
    public decimal? WfhMaxDaysPerMonth { get; set; }

    /// <summary>WFH phải nộp trước bao nhiêu ngày. Null = 1.</summary>
    public int? WfhMinNoticeDays { get; set; }

    /// <summary>Giới hạn số ngày cho 1 đơn WFH. Null = 30.</summary>
    public decimal? WfhMaxDaysPerRequest { get; set; }

    /// <summary>Độ dài tối thiểu của WorkPlan trong đơn WFH. Null = 20.</summary>
    public int? WfhWorkPlanMinLength { get; set; }

    // ===== Grace period cho auto-resolve AttendanceStatus =====

    /// <summary>
    /// Grace period đi muộn — check-in muộn ≤ N phút vẫn tính Present.
    /// Null = dùng default (5 phút).
    /// </summary>
    public int? LateGraceMinutes { get; set; }

    /// <summary>
    /// Grace period về sớm — check-out sớm ≤ M phút vẫn tính Present.
    /// Null = dùng default (5 phút).
    /// </summary>
    public int? EarlyLeaveGraceMinutes { get; set; }

    /// <summary>
    /// Ngưỡng nửa ngày — muộn hoặc về sớm ≥ K phút thì downgrade sang HalfDay.
    /// Null = dùng default (240 phút = 4h).
    /// </summary>
    public int? HalfDayThresholdMinutes { get; set; }

    public DateTime CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
