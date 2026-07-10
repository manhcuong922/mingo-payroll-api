using FluentValidation;
using MPS.Application.Features.Evaluation.MonthlyEvaluation.Commands;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Validators;

public class ScoreDirectMonthlyEvaluationCommandValidator
    : AbstractValidator<ScoreDirectMonthlyEvaluationCommand>
{
    public ScoreDirectMonthlyEvaluationCommandValidator()
    {
        RuleFor(x => x.RowVersion).NotEmpty().WithMessage("RowVersion là bắt buộc.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Cần ít nhất 1 tiêu chí để chấm.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Score)
                .InclusiveBetween(1, 5).WithMessage("Điểm mỗi tiêu chí phải từ 1 đến 5.");
            line.RuleFor(l => l.Note)
                .MaximumLength(2000).WithMessage("Ghi chú không vượt quá 2000 ký tự.");
        });
    }
}
