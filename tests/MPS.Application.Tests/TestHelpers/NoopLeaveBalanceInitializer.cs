using MPS.Application.Features.Attendance.LeaveBalance.Common;

namespace MPS.Application.Tests.TestHelpers;

/// <summary>
/// Test double cho <see cref="ILeaveBalanceInitializer"/> — cấp quỹ phép khi kích hoạt HĐ là
/// side-effect phụ, không thuộc hành vi đang kiểm ở các handler test, nên đây là no-op.
/// </summary>
public sealed class NoopLeaveBalanceInitializer : ILeaveBalanceInitializer
{
    public Task<bool> EnsureAnnualBalanceAsync(
        int employeeInformationId, int year, int actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
