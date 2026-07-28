using MPS.Application.Common.Payroll.Formula;

namespace MPS.Application.Common.Payroll;

/// <summary>
/// Dữ liệu gốc 1 nhân viên mà collector gom từ các module (theo khoá field). Calculator quy đổi
/// sang dict ResolvedValue (kèm nhãn từ catalog) để formula tham chiếu.
/// </summary>
public sealed class PayrollEmployeeInputs
{
    public required int EmployeeInformationId { get; init; }

    public Dictionary<string, decimal> Numbers { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Texts { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Dựng dict field (mọi khoá catalog, thiếu → 0/""), gắn nhãn để explanation.</summary>
    public IReadOnlyDictionary<string, ResolvedValue> ToFields(IPayrollFieldCatalog catalog)
    {
        var dict = new Dictionary<string, ResolvedValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in catalog.GetAll())
        {
            dict[f.Key] = f.IsText
                ? new ResolvedValue(0m, f.Label, Texts.GetValueOrDefault(f.Key, string.Empty))
                : new ResolvedValue(Numbers.GetValueOrDefault(f.Key, 0m), f.Label);
        }
        return dict;
    }

    public string ToSnapshotJson() => PayrollJson.Serialize(new InputSnapshot(Numbers, Texts));

    public static PayrollEmployeeInputs FromSnapshotJson(int employeeInformationId, string? json)
    {
        var snap = string.IsNullOrEmpty(json) ? null : PayrollJson.Deserialize<InputSnapshot>(json);
        return new PayrollEmployeeInputs
        {
            EmployeeInformationId = employeeInformationId,
            Numbers = snap?.Numbers ?? new(StringComparer.Ordinal),
            Texts = snap?.Texts ?? new(StringComparer.Ordinal),
        };
    }

    private sealed record InputSnapshot(Dictionary<string, decimal> Numbers, Dictionary<string, string> Texts);
}
