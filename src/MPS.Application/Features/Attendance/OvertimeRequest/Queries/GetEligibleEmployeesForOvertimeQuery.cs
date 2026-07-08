using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

/// <summary>
/// Trả về list NV thuộc Department của Project (loose scope) — dùng cho picker
/// khi PM tạo batch OT.
/// </summary>
public sealed record GetEligibleEmployeesForOvertimeQuery(int ProjectId)
    : IRequest<IReadOnlyList<EligibleEmployeeForOvertimeDto>>;
