using MPS.Application.Features.Attendance.OvertimeRequest.Commands;
using MPS.Domain.Enums.Attendance;

namespace MPS.API.Requests.Attendance;

/// <summary>Body cho NV self-create OT (luồng v3).</summary>
public sealed record CreateOvertimeRequestRequest(
    int ProjectId,
    DateOnly WorkDate,
    TimeOnly OtStartTime,
    TimeOnly OtEndTime,
    string? Reason);

/// <summary>Body Approver (HR/DM) Approve hoặc Reject — dùng chung 2 endpoint.</summary>
public sealed record ReviewOvertimeRequest(string? ReviewNote);

/// <summary>
/// Body PM Review: Forward (kèm ApproverRoute) hoặc Reject.
/// Rule cross-field: Forward → ApproverRoute non-null; Reject → ApproverRoute null.
/// Validator ở tầng Application chi tiết hơn.
/// </summary>
public sealed record PmReviewOvertimeRequestBody(
    PmReviewDecision Decision,
    OvertimeApproverRoute? ApproverRoute,
    string? Note);

/// <summary>NV tự sửa OT của mình khi còn Pending.</summary>
public sealed record UpdatePendingOvertimeRequest(
    DateOnly WorkDate,
    TimeOnly OtStartTime,
    TimeOnly OtEndTime,
    string? Reason);

/// <summary>Case 6 — Body tổ trưởng đăng ký OT hộ NV: kèm NV mục tiêu + route duyệt cuối.</summary>
public sealed record CreateOvertimeOnBehalfBody(
    int ProjectId,
    int EmployeeInformationId,
    DateOnly WorkDate,
    TimeOnly OtStartTime,
    TimeOnly OtEndTime,
    OvertimeApproverRoute ApproverRoute,
    string? Reason);
