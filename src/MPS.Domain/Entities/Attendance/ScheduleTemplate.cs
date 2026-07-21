using MPS.Domain.Enums.Attendance;

namespace MPS.Domain.Entities.Attendance;

public class ScheduleTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public int ShiftTemplateId { get; set; }
    public ShiftTemplate ShiftTemplate { get; set; } = null!;

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly EffectiveTo { get; set; }

    public ScheduleRepeatUnit RepeatUnit { get; set; } = ScheduleRepeatUnit.Week;
    public int RepeatEvery { get; set; } = 1;

    /// <summary>CSV of DayOfWeek ints (Sunday=0..Saturday=6). Only used when <c>RepeatUnit = Week</c>.</summary>
    public string? DaysOfWeekMask { get; set; }

    /// <summary>CSV of day-of-month ints (1..31). Only used when <c>RepeatUnit = Month</c> (deferred phase).</summary>
    public string? DaysOfMonthMask { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    /// <summary>
    /// User's persisted choice cho popup "Phân ca vào ngày nghỉ / lễ?" khi Save.
    /// true = "Có, vẫn phân" (BatchUpdate không skip WeeklyRestDay/PublicHoliday).
    /// false (default) = "Bỏ qua" (BatchUpdate skip). FE preview đọc field này để biết
    /// có tô hồng T7/CN/lễ trên calendar hay không.
    /// </summary>
    public bool IncludeRestDaysAndHolidays { get; set; }

    public ScheduleApplyTarget? ApplyTarget { get; set; }

    /// <summary>CSV of DepartmentId ints. Only used when <c>ApplyTarget = Departments</c>; NULL when Employees mode or legacy record.</summary>
    public string? TargetDepartmentIds { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    public ICollection<ScheduleTemplateEmployee> Employees { get; set; } = new List<ScheduleTemplateEmployee>();
    public ICollection<ScheduleTemplateShift> Shifts { get; set; } = new List<ScheduleTemplateShift>();
}
