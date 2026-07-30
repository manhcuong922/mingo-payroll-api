using MPS.Domain.Enums.Attendance;

namespace MPS.Domain.Entities.Attendance;

public class FaceRegistration
{
    public Guid Id { get; set; }

    public int EmployeeInformationId { get; set; }
    public EmployeeInformation EmployeeInformation { get; set; } = null!;

    public string PhotoObjectKey { get; set; } = null!;   // MinIO object key
    public string EmbeddingJson { get; set; } = null!;    // float[512] serialised as JSON

    public FaceRegistrationStatus Status { get; set; } = FaceRegistrationStatus.Pending;
    public string? RejectReason { get; set; }

    public DateTime SubmittedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
}
