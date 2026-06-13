namespace MPS.Domain.Entities.Compliance;

public sealed class OtRateEntry
{
    public int Id { get; set; }
    public int OtRateVersionId { get; set; }
    public OtRateVersion OtRateVersion { get; set; } = null!;

    public string OtType { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Rate { get; set; }
}
