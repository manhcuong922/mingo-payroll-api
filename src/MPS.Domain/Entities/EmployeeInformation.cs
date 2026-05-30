namespace MPS.Domain.Entities;

public class EmployeeInformation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string TaxNo { get; set; } = string.Empty;
    public string SocialNo { get; set; } = string.Empty;
    public string BankNumber { get; set; } = string.Empty;
    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;
    public int DependentsNo { get; set; }
    public string CitizenId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    public User User { get; set; } = null!;
}
