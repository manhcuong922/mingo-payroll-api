using FluentValidation;
using MPS.Application.Features.Project.Commands;

namespace MPS.Application.Features.Project.Validators;

public class ProjectCreateCommandValidator : AbstractValidator<ProjectCreateCommand>
{
    public ProjectCreateCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}
