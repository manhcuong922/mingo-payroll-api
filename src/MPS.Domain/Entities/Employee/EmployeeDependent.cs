using MPS.Domain.Entities;
using EmployeeEnums = MPS.Domain.Enums.Employee;

namespace MPS.Domain.Entities.Employee;

/// <summary>
/// Người phụ thuộc chính thức của employee — dùng cho tính giảm trừ gia cảnh (PIT).
/// Chỉ được tạo/cập nhật qua DependentRegistrationRequest (Approved).
/// </summary>
public class EmployeeDependent
{
    public int Id { get; set; }

    public int EmployeeInformationId { get; set; }
    public EmployeeInformation EmployeeInformation { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public EmployeeEnums.DependentRelationship Relationship { get; set; }

    /// <summary>Mã số thuế người phụ thuộc (10 hoặc 13 số). Có thể chưa có.</summary>
    public string? TaxIdentificationNumber { get; set; }

    /// <summary>Số CCCD/CMND (12 số). Có thể null với trẻ nhỏ.</summary>
    public string? CitizenId { get; set; }

    public DateOnly RegisteredFromDate { get; set; }
    public DateOnly? RegisteredToDate { get; set; }
    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
