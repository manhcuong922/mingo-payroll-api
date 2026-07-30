using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

/// <summary>
/// PM (tổ trưởng đơn = Project.ResponsibleUserId) review request OT của NV — luồng v3 §5.3.
///  - <see cref="Decision"/> = Forward → chuyển tiếp cho Approver cuối; phải kèm
///    <see cref="ApproverRoute"/> = Hr hoặc DepartmentManager.
///  - <see cref="Decision"/> = Reject → đóng request (Approver không thấy);
///    <see cref="ApproverRoute"/> phải null.
/// </summary>
public sealed record PmReviewOvertimeRequestCommand(
    int RequestId,
    PmReviewDecision Decision,
    OvertimeApproverRoute? ApproverRoute,
    string? Note) : IRequest<bool>;

public enum PmReviewDecision
{
    Forward = 1,
    Reject = 2,
}
