namespace MPS.Domain.Entities.Attendance;

/// <summary>
/// Loại nghỉ phép cấu hình động (FT-13). Thay cho enum cứng để HR tự CRUD.
/// Code là khóa nghiệp vụ ổn định (vd "ANNUAL", "SICK"); Name là tên hiển thị.
/// </summary>
public class LeaveType
{
    public int Id { get; set; }

    /// <summary>Khóa nghiệp vụ unique, dạng UPPER_SNAKE, vd "ANNUAL".</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>NSDLD trả lương trong ngày nghỉ hay không.</summary>
    public bool IsPaid { get; set; }

    /// <summary>Hệ số trả lương khi IsPaid=true. Null khi unpaid.</summary>
    public decimal? PaidRate { get; set; }

    /// <summary>Giới hạn ngày tối đa cho mỗi đơn nghỉ. Null = không giới hạn.</summary>
    public decimal? MaxDaysPerRequest { get; set; }

    public bool AllowEmployeeSelection { get; set; } = true;

    public decimal? MaxDaysPerYear { get; set; }

    public decimal? MaxDaysPerMonth { get; set; }

    public decimal? MaxDaysPerWeek { get; set; }

    public string ApplyScope { get; set; } = "AllEmployees";

    public string? TargetDepartmentIds { get; set; }

    public string? TargetEmployeeIds { get; set; }

    /// <summary>Loại nghỉ này có yêu cầu chứng từ đính kèm hay không.</summary>
    public bool RequireDocument { get; set; }

    /// <summary>True = có LeaveBalance/năm và Approve sẽ trừ balance (vd Annual).</summary>
    public bool DeductsBalance { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
