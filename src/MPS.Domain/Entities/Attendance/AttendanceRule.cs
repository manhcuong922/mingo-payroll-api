namespace MPS.Domain.Entities.Attendance;

public class AttendanceRule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal WorkRateNormal { get; set; } = 1.00m;
    public decimal WorkRateWeeklyRest { get; set; } = 2.00m;
    public decimal WorkRateHoliday { get; set; } = 3.00m;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
