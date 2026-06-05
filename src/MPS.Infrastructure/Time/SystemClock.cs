using MPS.Application.Common.Interfaces;

namespace MPS.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
