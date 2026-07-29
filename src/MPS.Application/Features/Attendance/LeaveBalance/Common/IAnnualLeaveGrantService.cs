namespace MPS.Application.Features.Attendance.LeaveBalance.Common;

/// <summary>
/// <c>[D22]</c> nửa (a) — cấp quỹ phép NĂM cho toàn bộ nhân viên. Job đầu năm gọi hàm này; admin
/// cũng trigger tay được qua <c>POST /api/admin/leave-balances/jobs/annual-grant/run</c>.
/// </summary>
public interface IAnnualLeaveGrantService
{
    Task<AnnualLeaveGrantResult> GrantForYearAsync(
        int year, int actorUserId, int? onlyEmployeeInformationId, CancellationToken cancellationToken);
}

/// <summary>
/// Kết quả một lượt cấp quỹ. <paramref name="Skipped"/> gồm NV/loại nghỉ không đủ điều kiện
/// (ngoài phạm vi áp dụng của loại nghỉ, hoặc quota tính ra 0 ngày).
/// </summary>
public sealed record AnnualLeaveGrantResult(
    int Year,
    int EmployeesProcessed,
    int Created,
    int Updated,
    int Skipped,
    int Failed);
