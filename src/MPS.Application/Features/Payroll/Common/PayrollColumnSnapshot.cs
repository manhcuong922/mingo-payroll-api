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
    /// <summary>
    /// ⭐ CC-2026-08 — CÔNG CHUẨN của kỳ lương: số ngày T2–T6 của tháng, <b>KHÔNG trừ ngày lễ</b>.
    ///
    /// <para>CONGCHUAN là MẪU SỐ của mọi phép chia lương. Trừ lễ ra (quy tắc #59/HOL-STD01 cũ) làm
    /// mẫu số co lại trong tháng nhiều lễ — tháng Tết còn 17 thay vì 22 — nên nghỉ 1 ngày bị trừ
    /// 1/17 thay vì 1/22: cùng một hành vi nghỉ mà tháng Tết bị phạt nặng hơn tháng thường ~30%.
    /// Đơn giá ngày công phải ổn định giữa các tháng.</para>
    ///
    /// <para>Ngày lễ KHÔNG biến mất khỏi phép tính — nó chuyển sang TỬ SỐ: <c>PayrollDataCollector</c>
    /// cộng mỗi ngày lễ T2–T6 thành 1 công hưởng lương (Đ.112 BLLĐ). Hai vế phải đi cùng nhau.</para>
    /// </summary>
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

}
