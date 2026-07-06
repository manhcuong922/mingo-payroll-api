using MediatR;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

/// <summary>
/// Employee tự chọn ngày và khung giờ OT. Hệ số/rate sẽ được tính sau dựa trên
/// các khung OT active, không bắt đơn phải phụ thuộc vào slot template.
/// </summary>
public sealed record CreateOvertimeRequestCommand(
    DateOnly WorkDate,
    TimeOnly OtStartTime,
    TimeOnly OtEndTime,
    string? Reason) : IRequest<int>;
