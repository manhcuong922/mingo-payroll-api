namespace MPS.Domain.Entities.Compliance;

public sealed class AllowanceExemptThresholdVersion
{
    public int Id { get; set; }
    public DateOnly ValidFrom { get; set; }
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }

    public int AllowanceTypeId { get; set; }
    public AllowanceType AllowanceType { get; set; } = null!;

    public string ThresholdMode { get; set; } = string.Empty;
    public decimal? MonthlyExemptAmount { get; set; }
    public bool AppliesToTax { get; set; }
    public bool AppliesToSocial { get; set; }

    public string LegalReference { get; set; } = string.Empty;
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
