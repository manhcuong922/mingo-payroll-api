using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Exceptions;

namespace MPS.Application.Features.Payroll.Common;

public static class PayrollFormulaValidationExtensions
{
    /// <summary>Chạy validator; nếu có lỗi → ném <see cref="DomainValidationException"/> gộp mọi lỗi.</summary>
    public static void EnsureValidOrThrow(
        this IPayrollFormulaValidator validator,
        IReadOnlyList<SalaryColumnDefinition> columns)
    {
        var errors = validator.Validate(columns);
        if (errors.Count > 0)
            throw new DomainValidationException("Công thức không hợp lệ: " + string.Join(" ", errors));
    }
}
