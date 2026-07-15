using MediatR;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

/// <summary>
/// Luồng OT v3 §5.1 (Q18) — form NV auto-derive đơn từ task NV được assign
/// (không lọc Status task). NV có thể được assign task ở nhiều đơn → trả ≥1 phần tử.
/// </summary>
public sealed record GetMyActiveOrdersForOtQuery : IRequest<IReadOnlyList<MyActiveOrderForOtDto>>;

public sealed record MyActiveOrderForOtDto(
    int OrderId,
    string OrderTitle,
    DateOnly StartDate,
    DateTime? Deadline,
    int? ResponsibleUserId,
    string? ResponsibleUserFullName,
    IReadOnlyList<MyActiveOrderTaskDto> AssignedTasks);

public sealed record MyActiveOrderTaskDto(
    int TaskId,
    string TaskTitle,
    DateTime? Deadline,
    int? Priority);
