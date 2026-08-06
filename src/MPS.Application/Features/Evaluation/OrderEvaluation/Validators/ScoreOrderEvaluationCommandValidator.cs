using FluentValidation;
using MPS.Application.Features.Evaluation.OrderEvaluation.Commands;

namespace MPS.Application.Features.Evaluation.OrderEvaluation.Validators;

public class ScoreOrderEvaluationCommandValidator : AbstractValidator<ScoreOrderEvaluationCommand>
{
    public ScoreOrderEvaluationCommandValidator()
    {
        RuleFor(x => x.RowVersion).NotEmpty().WithMessage("RowVersion là bắt buộc.");

        RuleFor(x => x.Subjects)
            .NotEmpty().WithMessage("Cần ít nhất 1 nhân viên để chấm.");

        // Subject không có Lines đã bị Controller lọc trước khi tạo command; ở đây chỉ còn subject có Lines.
        RuleForEach(x => x.Subjects).ChildRules(subject =>
        {
            subject.RuleForEach(s => s.Lines).ChildRules(line =>
            {
                line.RuleFor(l => l.Score)
                    .InclusiveBetween(1, 5).WithMessage("Điểm mỗi tiêu chí phải từ 1 đến 5.");
                line.RuleFor(l => l.Note)
                    .MaximumLength(2000).WithMessage("Ghi chú không vượt quá 2000 ký tự.");
            });
        });
    }
}
