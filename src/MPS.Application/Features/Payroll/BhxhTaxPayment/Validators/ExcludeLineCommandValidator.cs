using FluentValidation;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Commands;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Validators;

public sealed class ExcludeLineCommandValidator : AbstractValidator<ExcludeLineCommand>
{
    public ExcludeLineCommandValidator()
    {
        RuleFor(x => x.LineId).GreaterThan(0);

        RuleFor(x => x.ExcludedReason)
            .NotEmpty().WithMessage("Lý do bỏ qua là bắt buộc.")
            .MaximumLength(500).WithMessage("Lý do bỏ qua không được quá 500 ký tự.");

        RuleFor(x => x.RowVersion)
            .NotEmpty().WithMessage("Thiếu rowVersion — vui lòng tải lại.");
    }
}
