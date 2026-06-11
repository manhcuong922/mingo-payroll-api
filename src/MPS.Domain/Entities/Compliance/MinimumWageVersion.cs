namespace MPS.Domain.Entities.Compliance;

public class MinimumWageVersion
{
    public int Id { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool IsArchived { get; set; } = false;

    public string LegalReference { get; set; } = string.Empty;
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }

    // Phải có đủ 4 vùng I/II/III/IV
    public ICollection<MinimumWageRegion> Regions { get; set; } = new List<MinimumWageRegion>();
}
