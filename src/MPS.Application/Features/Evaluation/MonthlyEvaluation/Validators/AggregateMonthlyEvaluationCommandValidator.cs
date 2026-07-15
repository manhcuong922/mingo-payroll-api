using FluentValidation;
using MPS.Application.Features.Evaluation.MonthlyEvaluation.Commands;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Validators;

public class AggregateMonthlyEvaluationCommandValidator : AbstractValidator<AggregateMonthlyEvaluationCommand>
{
    public AggregateMonthlyEvaluationCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 3000).WithMessage("Năm không hợp lệ.");
        RuleFor(x => x.Month).InclusiveBetween(1, 12).WithMessage("Tháng phải từ 1 đến 12.");
    }
}
