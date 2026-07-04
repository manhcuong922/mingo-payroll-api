using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.Department.Queries;

// Danh sách nhân viên đủ điều kiện làm tổ trưởng xưởng (dropdown Department.ManagerId).
// Đã loại nhóm quản trị cấp công ty (SystemAdmin/HR/Accountant/Director/Ban KH) — xem SystemRoles.EligibleManagerRoleIds.
public sealed record DepartmentEligibleManagersQuery(string? Keyword = null)
    : IRequest<IReadOnlyList<EligibleManagerDto>>;
