using MPS.Application.DTOs.Payroll;
using MPS.Domain.Entities.Payroll;

namespace MPS.Application.Features.Payroll.Common;

/// <summary>Map entity cấu hình lương → DTO (enum → chuỗi cho client dễ đọc).</summary>
public static class PayrollConfigMapper
{
    public static SalaryColumnDto ToDto(SalaryColumnDefinition c) => new(
        c.Id,
        c.ColumnCode,
        c.Label,
        c.GroupName,
        c.DisplayOrder,
        c.DataType.ToString(),
        c.ValueSource.ToString(),
        c.LookupKey,
        c.FormulaJson,
        c.Rounding.ToString(),
        c.IsSystem,
        c.IsMoney,
        c.IsEditable);
}
