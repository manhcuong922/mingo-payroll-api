using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

/// <summary>
/// PM tạo batch OT request cho nhiều NV cùng project. Partial success — 1 NV
/// fail cap (vượt trần) hoặc scope không match sẽ được bỏ, các NV còn lại vẫn tạo.
/// </summary>
public sealed record CreateBatchOvertimeRequestCommand(
    int ProjectId,
    DateOnly WorkDate,
    TimeOnly OtStartTime,
    TimeOnly OtEndTime,
    IReadOnlyList<int> EmployeeInformationIds,
    OvertimeApproverRoute ApproverRoute,
    string? Reason) : IRequest<CreateBatchOvertimeRequestResult>;

public sealed record CreateBatchOvertimeRequestResult(
    Guid BatchId,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<CreateBatchItemResult> Results);

public sealed record CreateBatchItemResult(
    int EmployeeInformationId,
    string EmployeeFullName,
    bool Success,
    int? RequestId,
    string? Error);
