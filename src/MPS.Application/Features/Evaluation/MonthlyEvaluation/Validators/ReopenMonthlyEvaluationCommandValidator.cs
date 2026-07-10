using FluentValidation;
using MPS.Application.Features.Evaluation.MonthlyEvaluation.Commands;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Validators;

public class ReopenMonthlyEvaluationCommandValidator : AbstractValidator<ReopenMonthlyEvaluationCommand>
{
    public ReopenMonthlyEvaluationCommandValidator()
    {
        RuleFor(x => x.RowVersion).NotEmpty().WithMessage("RowVersion là bắt buộc.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Cần nêu lý do mở lại bảng.")
            .MaximumLength(2000).WithMessage("Lý do không vượt quá 2000 ký tự.");
    }
}
