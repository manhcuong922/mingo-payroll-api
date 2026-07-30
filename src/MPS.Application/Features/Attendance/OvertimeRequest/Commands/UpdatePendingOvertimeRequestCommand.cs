using MediatR;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

/// <summary>
/// NV tự sửa đơn OT của chính mình khi còn Pending (chưa PM review). Không đổi được:
///  - EmployeeInformationId
///  - ProjectId (nếu muốn đổi đơn: cancel + tạo lại)
///  - ApproverRoute (do PM chọn khi Forward, không phải NV)
///  - CreatedBy / CreatedAt / BatchId (legacy)
/// </summary>
public sealed record UpdatePendingOvertimeRequestCommand(
    int Id,
    DateOnly WorkDate,
    TimeOnly OtStartTime,
    TimeOnly OtEndTime,
    string? Reason) : IRequest<bool>;
