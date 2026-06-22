using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Infrastructure.Services;

public class ScheduleTemplateExpander : IScheduleTemplateExpander
{
    public IReadOnlyList<DateOnly> ExpandDates(ScheduleTemplate template)
    {
        if (template.EffectiveTo < template.EffectiveFrom)
        {
            return Array.Empty<DateOnly>();
        }
        if (template.RepeatEvery <= 0)
        {
            return Array.Empty<DateOnly>();
        }

        return template.RepeatUnit switch
        {
            ScheduleRepeatUnit.Day => ExpandDaily(template),
            ScheduleRepeatUnit.Week => ExpandWeekly(template),
            ScheduleRepeatUnit.Month => ExpandMonthly(template),
            _ => Array.Empty<DateOnly>()
        };
    }

    private static IReadOnlyList<DateOnly> ExpandDaily(ScheduleTemplate template)
    {
        var dates = new List<DateOnly>();
        var anchor = template.EffectiveFrom;
        for (var date = anchor; date <= template.EffectiveTo; date = date.AddDays(1))
        {
            var diff = date.DayNumber - anchor.DayNumber;
            if (diff % template.RepeatEvery == 0)
            {
                dates.Add(date);
            }
        }
        return dates;
    }

    private static IReadOnlyList<DateOnly> ExpandWeekly(ScheduleTemplate template)
    {
        var dows = ParseMask(template.DaysOfWeekMask).ToHashSet();
        if (dows.Count == 0)
        {
            return Array.Empty<DateOnly>();
        }

        // Anchor week starts at EffectiveFrom; "every N weeks" filters from there.
        var anchor = template.EffectiveFrom;
        var dates = new List<DateOnly>();
        for (var date = anchor; date <= template.EffectiveTo; date = date.AddDays(1))
        {
            if (!dows.Contains((int)date.DayOfWeek))
            {
                continue;
            }
            var weekIndex = (date.DayNumber - anchor.DayNumber) / 7;
            if (weekIndex % template.RepeatEvery == 0)
            {
                dates.Add(date);
            }
        }
        return dates;
    }

    private static IReadOnlyList<DateOnly> ExpandMonthly(ScheduleTemplate template)
    {
        var doms = ParseMask(template.DaysOfMonthMask).ToHashSet();
        if (doms.Count == 0)
        {
            return Array.Empty<DateOnly>();
        }

        var anchor = template.EffectiveFrom;
        var dates = new List<DateOnly>();
        for (var date = anchor; date <= template.EffectiveTo; date = date.AddDays(1))
        {
            if (!doms.Contains(date.Day))
            {
                continue;
            }
            var monthIndex = (date.Year - anchor.Year) * 12 + (date.Month - anchor.Month);
            if (monthIndex % template.RepeatEvery == 0)
            {
                dates.Add(date);
            }
        }
        return dates;
    }

    private static IEnumerable<int> ParseMask(string? mask)
    {
        if (string.IsNullOrWhiteSpace(mask))
        {
            yield break;
        }
        foreach (var part in mask.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var v))
            {
                yield return v;
            }
        }
    }
}
