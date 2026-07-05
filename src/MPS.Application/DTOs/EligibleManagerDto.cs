namespace MPS.Application.DTOs;

/// <summary>
/// Item cho dropdown chọn tổ trưởng xưởng (Department.ManagerId).
/// Chỉ gồm nhân viên có tài khoản (UserId != null) và mang role được phép
/// làm tổ trưởng (xem SystemRoles.EligibleManagerRoleIds).
/// </summary>
public sealed record EligibleManagerDto(
    int Id,
    string FullName,
    string? PositionName,
    int RoleId,
    string RoleName);
