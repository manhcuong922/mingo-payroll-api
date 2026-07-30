using MPS.Domain.Entities;
using EmployeeEnums = MPS.Domain.Enums.Employee;

namespace MPS.Domain.Entities.Employee;

/// <summary>
/// Đơn đăng ký / cập nhật / chấm dứt người phụ thuộc. HR duyệt. Khi Approved,
/// handler tạo/cập nhật EmployeeDependent + sync EmployeeInformation.DependentsNo.
/// </summary>
public class DependentRegistrationRequest
{
    public int Id { get; set; }

    public int EmployeeInformationId { get; set; }
    public EmployeeInformation EmployeeInformation { get; set; } = null!;

    public EmployeeEnums.DependentRequestType RequestType { get; set; }

    /// <summary>Chỉ set khi RequestType = Update / Deactivate.</summary>
    public int? TargetDependentId { get; set; }
    public EmployeeDependent? TargetDependent { get; set; }

    // ===== Payload (giống EmployeeDependent) =====
    public string FullName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public EmployeeEnums.DependentRelationship? Relationship { get; set; }
    public string? TaxIdentificationNumber { get; set; }
    public string? CitizenId { get; set; }
    public DateOnly? RegisteredFromDate { get; set; }
    public DateOnly? RegisteredToDate { get; set; }

    /// <summary>Bắt buộc cho Register — giấy khai sinh / CCCD người phụ thuộc.</summary>
    public string? SupportingDocumentUrl { get; set; }

    public EmployeeEnums.DependentRequestStatus Status { get; set; }

    public string? Reason { get; set; }
    public string? ReviewNote { get; set; }

    public int? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public DateTime? CancelledAt { get; set; }
    public int? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
