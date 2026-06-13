namespace MPS.Domain.Entities.Compliance;

public sealed class PitBracket
{
    public int Id { get; set; }
    public int PitRegulationVersionId { get; set; }
    public PitRegulationVersion PitRegulationVersion { get; set; } = null!;

    public int Order { get; set; }
    public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    public decimal Rate { get; set; }
}
