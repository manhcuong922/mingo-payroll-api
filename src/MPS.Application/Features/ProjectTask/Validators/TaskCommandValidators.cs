using FluentValidation;
using MPS.Application.Features.ProjectTask.Commands;
using MPS.Domain.Enums;

namespace MPS.Application.Features.ProjectTask.Validators;

public class TaskCreateCommandValidator : AbstractValidator<TaskCreateCommand>
{
    public TaskCreateCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("Task phải gắn xưởng.");
        RuleFor(x => x.AssigneeUserIds)
            .NotEmpty().WithMessage("Task phải có ít nhất 1 người thực hiện.");

        // Task Quantity: cần số lượng dương. Milestone: cần trọng số hợp lệ.
        When(x => x.ProgressType == ProgressType.Quantity, () =>
        {
            RuleFor(x => x.PlannedQuantity).GreaterThan(0)
                .WithMessage("Task theo sản lượng phải có số lượng lớn hơn 0.");
        });

        // Plan mục 6.3: Weight <= 0 khiến Task Milestone không bao giờ ảnh hưởng %, dễ gây hiểu nhầm "Done rồi mà % không tăng".
        When(x => x.ProgressType == ProgressType.Milestone, () =>
        {
            RuleFor(x => x.Weight).GreaterThan(0)
                .WithMessage("Task theo mốc phải có trọng số lớn hơn 0.");
        });
    }
}

public class TaskCreateConfidentialCommandValidator : AbstractValidator<TaskCreateConfidentialCommand>
{
    public TaskCreateConfidentialCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AssigneeUserIds)
            .NotEmpty().WithMessage("Task phải có ít nhất 1 người thực hiện.");
    }
}

public class TaskUpdateCommandValidator : AbstractValidator<TaskUpdateCommand>
{
    public TaskUpdateCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PlannedQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Weight).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AssigneeUserIds)
            .Must(list => list is null || list.Count > 0)
            .WithMessage("Nếu gửi danh sách người thực hiện thì phải có ít nhất 1 người.");
    }
}

public class TaskUpdateProgressCommandValidator : AbstractValidator<TaskUpdateProgressCommand>
{
    public TaskUpdateProgressCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.CompletedQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RowVersion).NotEmpty().WithMessage("Thiếu RowVersion để kiểm tra đồng thời.");
    }
}

public class TaskCompleteCommandValidator : AbstractValidator<TaskCompleteCommand>
{
    public TaskCompleteCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.RowVersion).NotEmpty().WithMessage("Thiếu RowVersion để kiểm tra đồng thời.");
    }
}

public class TaskDeleteCommandValidator : AbstractValidator<TaskDeleteCommand>
{
    public TaskDeleteCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
