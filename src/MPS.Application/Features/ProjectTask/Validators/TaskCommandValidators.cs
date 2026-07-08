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

        // Công đoạn (IsBatchParent) không được giao người trực tiếp; task lá cần ≥1 người — khớp
        // TaskCreateCommandHandler.ValidateAssignment (tránh 2 tầng validation mâu thuẫn nhau).
        When(x => !x.IsBatchParent, () =>
        {
            RuleFor(x => x.AssigneeUserIds)
                .NotEmpty().WithMessage("Task phải có ít nhất 1 người thực hiện.");
        }).Otherwise(() =>
        {
            RuleFor(x => x.AssigneeUserIds)
                .Empty().WithMessage("Công đoạn (task cha) không được giao người trực tiếp.");
        });

        // Task Quantity (công đoạn hoặc task lá sản lượng): cần số lượng dương.
        When(x => x.ProgressType == ProgressType.Quantity, () =>
        {
            RuleFor(x => x.PlannedQuantity).GreaterThan(0)
                .WithMessage("Task theo sản lượng phải có số lượng lớn hơn 0.");
        });

        // Công đoạn (IsBatchParent) cần PlannedWorkUnits dương — nếu không sẽ "vô hình" trong tiến độ Đơn
        // (docs/plans/plan.md mục 6.5: PlannedWorkUnits=0 → công đoạn bị loại khỏi mẫu số ProjectPercent).
        When(x => x.IsBatchParent, () =>
        {
            RuleFor(x => x.PlannedWorkUnits).GreaterThan(0)
                .WithMessage("Công đoạn phải có khối lượng công việc lớn hơn 0.");
        });

        // Gate/Checklist (Milestone) không cần số liệu — chỉ có Status Done/Todo + IsRequiredGate.
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
        RuleFor(x => x.PlannedWorkUnits).GreaterThanOrEqualTo(0);
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
