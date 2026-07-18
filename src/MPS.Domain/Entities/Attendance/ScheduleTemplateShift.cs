namespace MPS.Domain.Entities.Attendance;

public class ScheduleTemplateShift
{
    public int Id { get; set; }

    public int ScheduleTemplateId { get; set; }
    public ScheduleTemplate ScheduleTemplate { get; set; } = null!;

    public int ShiftTemplateId { get; set; }
    public ShiftTemplate ShiftTemplate { get; set; } = null!;
}
