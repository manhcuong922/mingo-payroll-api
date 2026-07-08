using FluentValidation;
using MPS.Application.Features.Project.Commands;
using MPS.Domain.Common;

namespace MPS.Application.Features.Project.Validators;

public class ProjectUpdateCommandValidator : AbstractValidator<ProjectUpdateCommand>
{
    public ProjectUpdateCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}

public class ProjectCompleteCommandValidator : AbstractValidator<ProjectCompleteCommand>
{
    public ProjectCompleteCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.CompletionNote).MaximumLength(4000)
            .When(x => !string.IsNullOrWhiteSpace(x.CompletionNote));
        RuleFor(x => x.CompletedAt)
            .LessThanOrEqualTo(_ => VietnamClock.Now)
            .When(x => x.CompletedAt.HasValue)
            .WithMessage("Ngày hoàn thành không được ở tương lai.");
    }
}

public class ProjectGenerateTasksCommandValidator : AbstractValidator<ProjectGenerateTasksCommand>
{
    public ProjectGenerateTasksCommandValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.ProcessTemplateId).GreaterThan(0);
    }
}
