using FluentValidation;
using MPS.Application.Features.ProjectTask.Commands;
using MPS.Domain.Enums;

namespace MPS.Application.Features.ProjectTask.Validators;

public class TaskCreateCommandValidator : AbstractValidator<TaskCreateCommand>
{
    public TaskCreateCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("Task must belong to a department.");

        When(x => x.IsBatchParent, () =>
        {
            RuleFor(x => x.AssigneeUserIds)
                .Empty().WithMessage("Stage cannot be assigned directly.");
            RuleFor(x => x.PlannedWorkUnits).GreaterThan(0)
                .WithMessage("Stage must have positive planned work units.");
        });

        When(x => !x.IsBatchParent && x.ProgressType == ProgressType.Quantity, () =>
        {
            RuleFor(x => x.AssigneeUserIds)
                .NotEmpty().WithMessage("Quantity task must have at least one assignee.");
        });

        When(x => x.ProgressType == ProgressType.Quantity, () =>
        {
            RuleFor(x => x.PlannedQuantity).GreaterThan(0)
                .WithMessage("Quantity task must have positive planned quantity.");
        });

        When(x => x.ProgressType == ProgressType.Milestone, () =>
        {
            RuleFor(x => x.PlannedQuantity).Equal(0)
                .WithMessage("Gate/Checklist must not have planned quantity.");
            RuleFor(x => x.PlannedWorkUnits).Equal(0)
                .WithMessage("Gate/Checklist must not have planned work units.");
        });
    }
}

public class TaskCreateConfidentialCommandValidator : AbstractValidator<TaskCreateConfidentialCommand>
{
    public TaskCreateConfidentialCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AssigneeUserIds)
            .NotEmpty().WithMessage("Task must have at least one assignee.");
    }
}

public class TaskUpdateCommandValidator : AbstractValidator<TaskUpdateCommand>
{
    public TaskUpdateCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PlannedQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PlannedWorkUnits).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AssigneeUserIds)
            .Must(list => list is null || list.Count > 0)
            .WithMessage("Assignee list cannot be empty when provided.");
    }
}

public class TaskUpdateProgressCommandValidator : AbstractValidator<TaskUpdateProgressCommand>
{
    public TaskUpdateProgressCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.CompletedQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RowVersion).NotEmpty().WithMessage("RowVersion is required.");
    }
}

public class TaskCompleteCommandValidator : AbstractValidator<TaskCompleteCommand>
{
    public TaskCompleteCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.RowVersion).NotEmpty().WithMessage("RowVersion is required.");
    }
}

public class TaskDeleteCommandValidator : AbstractValidator<TaskDeleteCommand>
{
    public TaskDeleteCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
