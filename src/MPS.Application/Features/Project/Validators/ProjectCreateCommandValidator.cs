using FluentValidation;
using MPS.Application.Features.Project.Commands;

namespace MPS.Application.Features.Project.Validators;

public class ProjectCreateCommandValidator : AbstractValidator<ProjectCreateCommand>
{
    public ProjectCreateCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity phải lớn hơn 0.");
        RuleFor(x => x.ResponsibleUserId).GreaterThan(0).WithMessage("Phải chọn tổ trưởng sản xuất.");
        RuleFor(x => x.Deadline)
            .Must((cmd, deadline) => deadline is null || cmd.StartDate is null
                || deadline.Value >= cmd.StartDate.Value.ToDateTime(TimeOnly.MinValue))
            .WithMessage("Deadline không được trước StartDate.");
    }
}
