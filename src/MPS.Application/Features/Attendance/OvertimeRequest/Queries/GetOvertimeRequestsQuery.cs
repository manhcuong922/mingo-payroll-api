using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

public sealed record GetOvertimeRequestsQuery(
    int? EmployeeInformationId,
    OtRequestStatus? Status,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int Page,
    int Size,
    int? CreatedByUserId = null,
    int? ProjectId = null,
    /// <summary>
    /// True → auto-filter list theo phạm vi duyệt của user (HR chỉ thấy route=Hr; DM chỉ
    /// thấy route=DepartmentManager cùng dept). Dùng cho trang approval inbox.
    /// False (default) → return mọi đơn user có quyền Read (attendance matrix, calendar…).
    /// </summary>
    bool MyScope = false) : IRequest<PagedResult<OvertimeRequestDto>>;
