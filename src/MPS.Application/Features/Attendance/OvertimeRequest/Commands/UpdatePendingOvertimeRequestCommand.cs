using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

/// <summary>
/// PM sửa đơn OT của chính mình khi còn Pending. Không đổi được:
///  - EmployeeInformationId (nếu muốn đổi NV thì cancel + tạo mới)
///  - ProjectId
///  - BatchId
///  - CreatedBy / CreatedAt
/// </summary>
public sealed record UpdatePendingOvertimeRequestCommand(
    int Id,
    DateOnly WorkDate,
    TimeOnly OtStartTime,
    TimeOnly OtEndTime,
    OvertimeApproverRoute ApproverRoute,
    string? Reason) : IRequest<bool>;
