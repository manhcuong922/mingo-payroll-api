namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Abstraction over <see cref="DateTime.UtcNow"/> so background jobs and time-sensitive
/// handlers stay unit-testable. Production registers <c>SystemClock</c>; tests inject
/// a fake that returns deterministic timestamps.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
