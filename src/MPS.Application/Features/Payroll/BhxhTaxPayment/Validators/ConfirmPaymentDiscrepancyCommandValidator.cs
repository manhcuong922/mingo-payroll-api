using FluentValidation;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Commands;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Validators;

public sealed class ConfirmPaymentDiscrepancyCommandValidator : AbstractValidator<ConfirmPaymentDiscrepancyCommand>
{
    public ConfirmPaymentDiscrepancyCommandValidator()
    {
        RuleFor(x => x.ObligationId).GreaterThan(0);

        RuleFor(x => x.ConfirmationNote)
            .MaximumLength(1000)
            .WithMessage("Ghi chú xác nhận không được quá 1000 ký tự.");
    }
}
