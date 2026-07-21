using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Tests.TestHelpers;

/// <summary>
/// Test double cho <see cref="IPayrollSourceChangeHandler"/> — mark stale + capture adjustment là
/// side-effect phụ, không thuộc hành vi đang kiểm ở các handler test, nên đây là no-op.
/// </summary>
public sealed class NoopPayrollSourceChangeHandler : IPayrollSourceChangeHandler
{
    public Task OnSourceChangedAsync(
        IEnumerable<(int Year, int Month)> months,
        IReadOnlyCollection<int> employeeInformationIds,
        string reason,
        CancellationToken ct = default) => Task.CompletedTask;

    public Task OnSourceChangedAsync(
        int year, int month, int employeeInformationId, string reason, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task OnEffectiveFromChangedAsync(
        int year, int month, int employeeInformationId, string reason, CancellationToken ct = default)
        => Task.CompletedTask;
}
