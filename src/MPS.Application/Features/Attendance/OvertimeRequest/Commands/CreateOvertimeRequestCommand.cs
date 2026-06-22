using MediatR;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

public sealed record CreateOvertimeRequestCommand(
    DateOnly WorkDate,
    TimeOnly OtStartTime,
    TimeOnly OtEndTime,
    string? Reason) : IRequest<int>;
