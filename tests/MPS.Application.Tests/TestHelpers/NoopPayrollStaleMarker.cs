using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Tests.TestHelpers;

/// <summary>
/// Test double cho <see cref="IPayrollStaleMarker"/> — đánh cờ stale là side-effect phụ, không thuộc
/// hành vi đang kiểm ở các handler test, nên đây là no-op.
/// </summary>
public sealed class NoopPayrollStaleMarker : IPayrollStaleMarker
{
    public Task MarkStaleForMonthAsync(int year, int month, string reason, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task MarkStaleForMonthsAsync(
        IEnumerable<(int Year, int Month)> months, string reason, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task MarkStaleFromMonthAsync(int year, int month, string reason, CancellationToken ct = default)
        => Task.CompletedTask;
}
