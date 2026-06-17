namespace MPS.Domain.Entities.Compliance;

public sealed class SocialInsuranceFundEntry
{
    public int Id { get; set; }
    public int SocialInsuranceRateVersionId { get; set; }
    public SocialInsuranceRateVersion SocialInsuranceRateVersion { get; set; } = null!;

    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal EmployeePercent { get; set; }
    public decimal EmployerPercent { get; set; }
}
