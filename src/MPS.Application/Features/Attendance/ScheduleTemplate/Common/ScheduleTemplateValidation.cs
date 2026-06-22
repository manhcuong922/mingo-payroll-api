using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Common;

internal static class ScheduleTemplateValidation
{
    private const int MaxRangeDays = 366;
    private const int MaxRepeatEvery = 52;

    public static void EnsureValidShape(
        DateOnly effectiveFrom,
        DateOnly effectiveTo,
        ScheduleRepeatUnit repeatUnit,
        int repeatEvery,
        IReadOnlyList<int>? daysOfWeek,
        IReadOnlyList<int>? daysOfMonth,
        IReadOnlyList<int> employeeIds)
    {
        if (effectiveTo < effectiveFrom)
        {
            throw new DomainValidationException("EffectiveTo phải >= EffectiveFrom.");
        }
        var spanDays = effectiveTo.DayNumber - effectiveFrom.DayNumber + 1;
        if (spanDays > MaxRangeDays)
        {
            throw new DomainValidationException($"BR-36: khoảng EffectiveFrom..EffectiveTo tối đa {MaxRangeDays} ngày, hiện {spanDays}.");
        }
        if (repeatEvery < 1 || repeatEvery > MaxRepeatEvery)
        {
            throw new DomainValidationException($"BR-34: RepeatEvery phải trong [1, {MaxRepeatEvery}].");
        }
        if (employeeIds is null || employeeIds.Count == 0)
        {
            throw new DomainValidationException("Phải chọn ít nhất 1 nhân viên áp dụng.");
        }

        switch (repeatUnit)
        {
            case ScheduleRepeatUnit.Week:
                var dow = daysOfWeek ?? Array.Empty<int>();
                if (dow.Count == 0)
                {
                    throw new DomainValidationException("BR-35: Lặp theo Tuần yêu cầu chọn ít nhất 1 thứ trong tuần.");
                }
                if (dow.Any(d => d < 0 || d > 6))
                {
                    throw new DomainValidationException("BR-35: DaysOfWeek phải nằm trong [0=CN..6=T7].");
                }
                break;
            case ScheduleRepeatUnit.Month:
                var dom = daysOfMonth ?? Array.Empty<int>();
                if (dom.Count == 0)
                {
                    throw new DomainValidationException("Lặp theo Tháng yêu cầu chọn ít nhất 1 ngày trong tháng.");
                }
                if (dom.Any(d => d < 1 || d > 31))
                {
                    throw new DomainValidationException("DaysOfMonth phải nằm trong [1..31].");
                }
                break;
            case ScheduleRepeatUnit.Day:
                break;
        }
    }

    public static string? ToCsvMask(IReadOnlyList<int>? values)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }
        return string.Join(',', values.Distinct().OrderBy(v => v));
    }

    public static IReadOnlyList<int> FromCsvMask(string? mask)
    {
        if (string.IsNullOrWhiteSpace(mask))
        {
            return Array.Empty<int>();
        }
        return mask.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => int.Parse(p))
            .OrderBy(v => v)
            .ToList();
    }
}
