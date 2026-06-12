using MPS.Domain.Enums.Employee;

namespace MPS.Domain.Entities;

public class EmployeeInformation
{
    public int Id { get; set; }
    public int? UserId { get; set; }

    // Personal information — single source of truth for NLĐ identity
    public string FullName { get; set; } = string.Empty;
    public DateTime Dob { get; set; }
    public Gender Gender { get; set; }
    public string CitizenId { get; set; } = string.Empty;
    public DateTime? CitizenIssuedDate { get; set; }
    public string? CitizenIssuedPlace { get; set; }
    public string PermanentAddress { get; set; } = string.Empty;
    public string? ContactAddress { get; set; }
    public string? Email { get; set; }
    public string? PhoneNo { get; set; }
    public string? EducationLevel { get; set; }

    // Employment info
    public int PositionId { get; set; }
    public int DependentsNo { get; set; }

    // Financial — nullable, may not be filled at contract creation time
    public string? TaxNo { get; set; }
    public string? SocialNo { get; set; }
    public string? BankNumber { get; set; }

    // Navigation
    public Position Position { get; set; } = null!;
    public User? User { get; set; }
}
