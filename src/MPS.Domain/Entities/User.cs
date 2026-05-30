namespace MPS.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime Dob { get; set; }
    public string PhoneNo { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? PasswordChangedAt { get; set; }

    public EmployeeInformation? EmployeeInformation { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
