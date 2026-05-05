namespace MPS.Application.DTOs;

public record UserDto(
    string EmployeeCode,
    string FullName,
    int RoleId,
    string RoleName,
    string Username,
    string Email,
    DateTime Dob,
    string PhoneNo,
    int Status);