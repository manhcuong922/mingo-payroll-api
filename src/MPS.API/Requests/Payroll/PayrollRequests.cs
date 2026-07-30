namespace MPS.API.Requests.Payroll;

public sealed record CreatePayrollPeriodRequest(
    int Year,
    int Month,
    int? FormulaSetId,
    decimal? StandardWorkingDays);

public sealed record EditPayrollCellRequest(
    int RowId,
    string ColumnCode,
    decimal? OverrideValue,
    string? OverrideText = null);
