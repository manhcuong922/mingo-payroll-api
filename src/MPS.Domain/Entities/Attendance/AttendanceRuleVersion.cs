namespace MPS.Domain.Entities.Attendance;

/// <summary>
/// Phiên bản hệ số công theo thời gian cho 1 AttendanceRule.
/// Timeline không overlap; tại 1 thời điểm mỗi rule có tối đa 1 version với EffectiveTo = NULL (open).
/// </summary>
public class AttendanceRuleVersion
{
    public int Id { get; set; }

    public int AttendanceRuleId { get; set; }
    public AttendanceRule AttendanceRule { get; set; } = null!;

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    public decimal WorkRateNormal { get; set; }
    public decimal WorkRateWeeklyRest { get; set; }
    public decimal WorkRateHoliday { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
