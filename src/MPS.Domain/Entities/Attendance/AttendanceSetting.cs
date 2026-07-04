namespace MPS.Domain.Entities.Attendance;

public class AttendanceSetting
{
    public int Id { get; set; }

    public bool IsEditLockEnabled { get; set; }
    public TimeOnly DailyEditCutoffTime { get; set; }
    public int LockAfterDays { get; set; }
    public bool AllowAdminOverride { get; set; }
    public bool RequireOverrideReason { get; set; }
    public string TimezoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
