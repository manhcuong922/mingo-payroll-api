using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Domain.Entities.Attendance;

public class AttendanceAdjustment
{
    public int Id { get; set; }

    public int AttendanceRecordId { get; set; }
    public AttendanceRecord AttendanceRecord { get; set; } = null!;

    public int EmployeeInformationId { get; set; }
    public EmployeeInformation EmployeeInformation { get; set; } = null!;

    public AttendanceEnums.AttendanceStatus NewStatus { get; set; }
    public TimeOnly? NewCheckInTime { get; set; }
    public TimeOnly? NewCheckOutTime { get; set; }

    public string Reason { get; set; } = string.Empty;

    public AttendanceEnums.AdjustmentStatus Status { get; set; }
    public string? ReviewNote { get; set; }

    public int? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public AttendanceEnums.AttendanceStatus? OriginalStatus { get; set; }
    public TimeOnly? OriginalCheckInTime { get; set; }
    public TimeOnly? OriginalCheckOutTime { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}