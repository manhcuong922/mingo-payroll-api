namespace MPS.Application.DTOs;

public sealed record CurrentUserDto(
    int Id,
    string EmployeeCode,
    string FullName,
    int RoleId,
    string RoleName,
    string Username,
    string Email,
    DateTime Dob,
    string PhoneNo,
    int Status,
    DateTime? LastLoginAt,
    IReadOnlyCollection<string> Permissions);
