using MPS.Domain.Enums.Employee;

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
    /// <summary>
    /// Giới tính lấy từ EmployeeInformation (join theo UserId). Null với user không có
    /// EmployeeInformation (vd system admin). FE dùng để filter LeaveType MATERNITY
    /// đối với nhân viên nam.
    /// </summary>
    Gender? Gender,
    int Status,
    DateTime? LastLoginAt,
    IReadOnlyCollection<string> Permissions);
