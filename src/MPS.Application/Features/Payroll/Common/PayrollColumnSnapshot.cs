using MPS.Application.Common.Payroll;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Features.Payroll.Common;

/// <summary>
/// Snapshot 1 cột lưu vào kỳ (bất biến) — đủ để tái dựng SalaryColumnDefinition khi recompute,
/// không phụ thuộc bảng công thức gốc (có thể đã bị sửa sau).
/// </summary>
public sealed record PayrollColumnSnapshot(
    string ColumnCode, string Label, string? GroupName, int DisplayOrder,
    int DataType, int ValueSource, string? LookupKey, string? FormulaJson,
    int Rounding, bool IsMoney, bool IsEditable);

public static class PayrollColumnSnapshotMapper
{
    public static List<PayrollColumnSnapshot> ToSnapshot(IEnumerable<SalaryColumnDefinition> columns) =>
        columns.OrderBy(c => c.DisplayOrder).Select(c => new PayrollColumnSnapshot(
            c.ColumnCode, c.Label, c.GroupName, c.DisplayOrder,
            (int)c.DataType, (int)c.ValueSource, c.LookupKey, c.FormulaJson,
            (int)c.Rounding, c.IsMoney, c.IsEditable)).ToList();

    public static List<SalaryColumnDefinition> ToDefinitions(IEnumerable<PayrollColumnSnapshot> snapshots) =>
        snapshots.Select(s => new SalaryColumnDefinition
        {
            ColumnCode = s.ColumnCode,
            Label = s.Label,
            GroupName = s.GroupName,
            DisplayOrder = s.DisplayOrder,
            DataType = (ColumnDataType)s.DataType,
            ValueSource = (ColumnValueSource)s.ValueSource,
            LookupKey = s.LookupKey,
            FormulaJson = s.FormulaJson,
            Rounding = (RoundingMode)s.Rounding,
            IsMoney = s.IsMoney,
            IsEditable = s.IsEditable,
        }).ToList();

    public static string Serialize(IEnumerable<SalaryColumnDefinition> columns) =>
        PayrollJson.Serialize(ToSnapshot(columns));

    public static List<SalaryColumnDefinition> Deserialize(string json) =>
        ToDefinitions(PayrollJson.Deserialize<List<PayrollColumnSnapshot>>(json) ?? new());
}

/// <summary>Tiện ích tính công chuẩn mặc định của tháng.</summary>
public static class PayrollWorkingDays
{
    /// <summary>Số ngày T2–T6 của tháng (KHÔNG trừ lễ). Giữ lại cho các phép đối chiếu/không có lễ.</summary>
    public static decimal CountWeekdays(int year, int month)
    {
        var days = DateTime.DaysInMonth(year, month);
        var count = 0;
        for (var d = 1; d <= days; d++)
        {
            var dow = new DateOnly(year, month, d).DayOfWeek;
            if (dow != DayOfWeek.Saturday && dow != DayOfWeek.Sunday) count++;
        }
        return count;
    }

    /// <summary>
    /// #59 — Công chuẩn mặc định = số ngày T2–T6 TRỪ ngày lễ. Lễ = mốc dương lịch cố định theo Đ.112
    /// (luôn trừ, kể cả bảng <c>PublicHolidays</c> còn trống) ∪ ngày lễ đã nhập trong DB
    /// (<paramref name="enteredHolidays"/>, gồm cả Tết/Giỗ Tổ âm lịch mà HR nhập tay).
    /// CONGCHUAN là MẪU SỐ của mọi phép chia lương — đặt cao hơn thực tế ⇒ mỗi ngày công rẻ đi.
    /// </summary>
    public static decimal CountStandardWorkingDays(int year, int month, IEnumerable<DateOnly>? enteredHolidays = null)
    {
        var holidays = new HashSet<DateOnly>(LaborLawConstants.PublicHolidays.FixedSolarInMonth(year, month));
        if (enteredHolidays is not null)
            foreach (var h in enteredHolidays) holidays.Add(h);

        var totalDays = DateTime.DaysInMonth(year, month);
        var count = 0;
        for (var d = 1; d <= totalDays; d++)
        {
            var date = new DateOnly(year, month, d);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            if (holidays.Contains(date)) continue;
            count++;
        }
        return count;
    }

    /// <summary>Bung các khoảng ngày lễ (<see cref="PublicHoliday"/>) thành tập DateOnly nằm trong tháng.</summary>
    public static IReadOnlyCollection<DateOnly> HolidayDatesInMonth(
        IEnumerable<PublicHoliday> holidays, int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var set = new HashSet<DateOnly>();
        foreach (var h in holidays)
        {
            var end = h.HolidayEndDate < h.HolidayDate ? h.HolidayDate : h.HolidayEndDate;
            for (var d = h.HolidayDate; d <= end; d = d.AddDays(1))
            {
                if (d < first) continue;
                if (d > last) break;
                set.Add(d);
            }
        }
        return set;
    }
}
