using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

/// <summary>
/// Trả về list Project thuộc Department mà PM đang trực thuộc. Dùng cho dropdown
/// khi PM tạo batch OT.
/// </summary>
public sealed record GetPmProjectsQuery : IRequest<IReadOnlyList<PmProjectDto>>;
