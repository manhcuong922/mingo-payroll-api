namespace MPS.Domain.Entities.Compliance;

public class PitBracket
{
    public int Id { get; set; }
    public int PitRegulationVersionId { get; set; }
    public PitRegulationVersion PitRegulationVersion { get; set; } = null!;

    // Thứ tự bậc, bắt đầu từ 1
    public int Order { get; set; }
    // Cận dưới inclusive, đơn vị VND/tháng. Order=1 thì = 0
    public decimal LowerBound { get; set; }
    // Cận trên exclusive. Bracket cuối = null (vô cực)
    public decimal? UpperBound { get; set; }
    // Thuế suất, vd 0.0500 (5%), 0.3500 (35%)
    public decimal Rate { get; set; }
}
