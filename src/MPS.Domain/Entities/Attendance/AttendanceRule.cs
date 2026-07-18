namespace MPS.Domain.Entities.Attendance;

public class AttendanceRule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    // Lịch sử hệ số theo thời gian (không overlap). Truy vấn qua IAttendanceRuleVersionRepository.
    public ICollection<AttendanceRuleVersion> Versions { get; set; } = new List<AttendanceRuleVersion>();
}
