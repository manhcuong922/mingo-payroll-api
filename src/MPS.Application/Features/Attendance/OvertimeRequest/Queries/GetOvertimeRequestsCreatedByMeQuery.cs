using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

/// <summary>
/// PM xem danh sách OT do chính PM đã gửi (createdByUserId = current user).
/// Khác <see cref="GetMyOvertimeRequestsQuery"/> vốn filter theo employeeId (OT NV nhận),
/// query này filter theo user tạo đơn (OT PM giao đi).
/// </summary>
public sealed record GetOvertimeRequestsCreatedByMeQuery(
    OtRequestStatus? Status,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int? ProjectId,
    int Page,
    int Size) : IRequest<PagedResult<OvertimeRequestDto>>;
