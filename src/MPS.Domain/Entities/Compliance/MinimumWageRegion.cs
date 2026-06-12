namespace MPS.Domain.Entities.Compliance;

public sealed class MinimumWageRegion
{
    public int Id { get; set; }
    public int MinimumWageVersionId { get; set; }
    public MinimumWageVersion MinimumWageVersion { get; set; } = null!;

    public string RegionCode { get; set; } = string.Empty;
    public decimal AmountPerMonth { get; set; }
}
