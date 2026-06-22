namespace MPS.Domain.Entities.Attendance;

public class ScheduleTemplateEmployee
{
    public int Id { get; set; }

    public int ScheduleTemplateId { get; set; }
    public ScheduleTemplate ScheduleTemplate { get; set; } = null!;

    public int EmployeeInformationId { get; set; }
    public EmployeeInformation EmployeeInformation { get; set; } = null!;
}
