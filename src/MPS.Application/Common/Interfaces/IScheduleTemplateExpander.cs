using MPS.Domain.Entities.Attendance;

namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Pure date expansion service — turns a <see cref="ScheduleTemplate"/> into
/// the concrete list of work dates within <c>EffectiveFrom..EffectiveTo</c>.
/// Knows nothing about employees or existing assignments.
/// </summary>
public interface IScheduleTemplateExpander
{
    IReadOnlyList<DateOnly> ExpandDates(ScheduleTemplate template);
}
