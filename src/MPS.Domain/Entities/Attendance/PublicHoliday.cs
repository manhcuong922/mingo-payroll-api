namespace MPS.Domain.Entities.Attendance;

public class PublicHoliday
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly HolidayDate { get; set; }
    public DateOnly HolidayEndDate { get; set; }
    public string? Description { get; set; }
    public string ApplyScope { get; set; } = "AllEmployees";
    public string? TargetDepartmentIds { get; set; }
    public string? TargetEmployeeIds { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
