namespace MPS.Domain.Interfaces;

public interface IComplianceVersion
{
    int Id { get; }
    DateOnly ValidFrom { get; set; }
    bool IsActive { get; set; }
    bool IsArchived { get; set; }
    DateTime? UpdatedAt { get; set; }
}
