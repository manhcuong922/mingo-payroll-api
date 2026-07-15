using MPS.Domain.Enums.Payroll;

namespace MPS.API.Requests.Payroll;

// ===== Bảng công thức =====
public sealed record CreateFormulaSetRequest(string Name, string? Description);
public sealed record UpdateFormulaSetRequest(string Name, string? Description, bool IsActive);
public sealed record CloneFormulaSetRequest(string NewName, string? Description);

// ===== Cột =====
public sealed record AddColumnRequest(
    string ColumnCode,
    string Label,
    string? GroupName,
    int? DisplayOrder,
    ColumnDataType DataType,
    ColumnValueSource ValueSource,
    string? LookupKey,
    string? FormulaJson,
    RoundingMode Rounding,
    bool IsMoney,
    bool IsEditable);

public sealed record UpdateColumnRequest(
    string Label,
    string? GroupName,
    int DisplayOrder,
    ColumnDataType DataType,
    ColumnValueSource ValueSource,
    string? LookupKey,
    string? FormulaJson,
    RoundingMode Rounding,
    bool IsMoney,
    bool IsEditable);

public sealed record ReorderColumnsRequest(IReadOnlyList<int> OrderedColumnIds);

// ===== Tham số công ty =====
public sealed record CreatePayrollParameterRequest(string Key, string Name, decimal Value, string? Description);
public sealed record UpdatePayrollParameterRequest(string Name, decimal Value, string? Description, bool IsActive);

// ===== Hệ số năng suất =====
public sealed record CreateProductivityCoefficientRequest(decimal MinScore, decimal MaxScore, decimal Coefficient, string? Label);
public sealed record UpdateProductivityCoefficientRequest(decimal MinScore, decimal MaxScore, decimal Coefficient, string? Label, bool IsActive);
