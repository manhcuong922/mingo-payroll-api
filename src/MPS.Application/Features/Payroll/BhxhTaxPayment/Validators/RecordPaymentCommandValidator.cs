using FluentValidation;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Commands;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Validators;

public sealed class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(x => x.ObligationId).GreaterThan(0);

        RuleFor(x => x.PaidAmount)
            .GreaterThan(0m)
            .WithMessage("Số tiền thực nộp phải lớn hơn 0.");

        RuleFor(x => x.PaidDate)
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1))
            .WithMessage("Ngày nộp không được ở tương lai.");

        RuleFor(x => x.PaymentMethod)
            .IsInEnum()
            .WithMessage("Hình thức nộp không hợp lệ.");

        RuleFor(x => x.TransactionRef)
            .MaximumLength(100)
            .WithMessage("Mã giao dịch không được quá 100 ký tự.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000);
    }
}
