using FluentValidation;
using MPS.Application.Features.ProcessTemplate.Commands;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProcessTemplate.Validators;

public class ProcessTemplateCreateCommandValidator : AbstractValidator<ProcessTemplateCreateCommand>
{
    public ProcessTemplateCreateCommandValidator(IProcessTemplateRepository repository)
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(20).WithMessage("Code must not exceed 20 characters.")
            .Matches("^[A-Za-z0-9_\\-]+$").WithMessage("Code may only contain letters, digits, hyphen and underscore.")
            .MustAsync(async (code, ct) => !await repository.CodeExistsAsync(code.Trim().ToUpperInvariant(), null, ct))
            .WithMessage("Process template code already exists.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name must not exceed 150 characters.");

        RuleFor(x => x.Steps)
            .NotEmpty().WithMessage("At least one step is required.");

        // Step Quantity sinh công đoạn — cần cả sản lượng kế hoạch lẫn khối lượng công việc (tỷ trọng lên
        // đơn) dương, nếu không công đoạn sinh ra sẽ "vô hình" trong tiến độ đơn.
        // Step Milestone/Gate sinh gate — KHÔNG có sản lượng/khối lượng công việc (plan.md mục 5.3/8.4).
        RuleForEach(x => x.Steps).ChildRules(step =>
        {
            step.RuleFor(s => s.Name).NotEmpty().MaximumLength(150);

            step.When(s => s.DefaultProgressType == ProgressType.Quantity, () =>
            {
                step.RuleFor(s => s.DefaultPlannedQuantity).GreaterThan(0)
                    .WithMessage("Sản lượng kế hoạch mặc định phải lớn hơn 0.");
                step.RuleFor(s => s.DefaultPlannedWorkUnits).GreaterThan(0)
                    .WithMessage("Khối lượng công việc mặc định phải lớn hơn 0.");
            }).Otherwise(() =>
            {
                step.RuleFor(s => s.DefaultPlannedQuantity).Equal(0)
                    .WithMessage("Gate không có sản lượng kế hoạch.");
                step.RuleFor(s => s.DefaultPlannedWorkUnits).Equal(0)
                    .WithMessage("Gate không có khối lượng công việc.");
            });
        });
    }
}
