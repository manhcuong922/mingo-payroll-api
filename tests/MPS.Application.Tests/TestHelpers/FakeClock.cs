using MPS.Application.Common.Interfaces;

namespace MPS.Application.Tests.TestHelpers;

/// <summary>
/// Deterministic <see cref="IClock"/> for tests. UtcNow là mutable để test có thể "nhảy thời gian"
/// (jump forward khi cần verify job promote PendingTermination → Terminated).
/// </summary>
public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; }

    public FakeClock() : this(new DateTime(2026, 6, 15, 3, 0, 0, DateTimeKind.Utc)) { }

    public FakeClock(DateTime utcNow) => UtcNow = utcNow;
}
