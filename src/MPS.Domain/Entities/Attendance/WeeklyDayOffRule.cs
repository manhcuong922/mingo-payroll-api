namespace MPS.Domain.Entities.Attendance;

public class WeeklyDayOffRule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>CSV các ngày trong tuần theo convention 0=Sunday..6=Saturday (đồng nhất FE + System.DayOfWeek).</summary>
    public string DaysOfWeekMask { get; set; } = string.Empty;

    public string ApplyScope { get; set; } = "AllEmployees";
    public string? TargetDepartmentIds { get; set; }
    public string? TargetEmployeeIds { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
