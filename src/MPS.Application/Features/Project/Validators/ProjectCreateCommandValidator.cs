using FluentValidation;
using MPS.Application.Features.Project.Commands;

namespace MPS.Application.Features.Project.Validators;

public class ProjectCreateCommandValidator : AbstractValidator<ProjectCreateCommand>
{
    public ProjectCreateCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0.");
    }
}
