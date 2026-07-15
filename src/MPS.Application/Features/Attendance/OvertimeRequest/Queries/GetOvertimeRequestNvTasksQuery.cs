using MediatR;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

/// <summary>
/// Luồng OT v3 — trả list task NV (người tạo request) đang được assign
/// trong đơn của request đó. Dùng ở PM Inbox và Approver detail để làm context
/// khi review/duyệt.
/// </summary>
public sealed record GetOvertimeRequestNvTasksQuery(int RequestId)
    : IRequest<IReadOnlyList<MyActiveOrderTaskDto>>;
