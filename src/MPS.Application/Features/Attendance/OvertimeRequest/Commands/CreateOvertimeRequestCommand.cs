using MediatR;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

/// <summary>
/// NV tự đăng ký OT trong đơn mình đang có task Status=InProgress. Luồng v3 §5.2:
///  - <see cref="ProjectId"/>: đơn NV có task InProgress (auto-derive ở FE qua GetMyActiveOrdersForOtQuery).
///  - <see cref="OtStartTime"/>/<see cref="OtEndTime"/>: khung giờ OT (FE map từ OvertimeSlotTemplate).
///  - Status khởi tạo = Pending (chờ PM tổ trưởng đơn review).
/// </summary>
public sealed record CreateOvertimeRequestCommand(
    int ProjectId,
    DateOnly WorkDate,
    TimeOnly OtStartTime,
    TimeOnly OtEndTime,
    string? Reason) : IRequest<int>;
