using FluentValidation;
using MPS.Application.Features.PerformanceMetric.Commands;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.PerformanceMetric.Validators;

public class PerformanceMetricUpdateCommandValidator : AbstractValidator<PerformanceMetricUpdateCommand>
{
    public PerformanceMetricUpdateCommandValidator(
        IPerformanceMetricRepository repository,
        ITypePerformanceMetricRepository typeRepository)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be greater than 0.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotNull().WithMessage("Description is required.")
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");

        RuleFor(x => x.Weight)
            .GreaterThan(0).WithMessage("Weight must be greater than 0.")
            .LessThanOrEqualTo(100).WithMessage("Weight must not exceed 100.");

        RuleFor(x => x.TypePerformanceMetricId)
            .GreaterThan(0).WithMessage("TypePerformanceMetricId is required.")
            .MustAsync(async (id, ct) => await typeRepository.GetByIdAsync(id, ct) is not null)
            .WithMessage("TypePerformanceMetricId does not exist.");

        RuleFor(x => x.TargetRoleIds)
            .NotNull().WithMessage("TargetRoleIds is required.")
            .MustAsync(async (ids, ct) => await repository.AllRolesExistAsync(ids, ct))
            .WithMessage("One or more TargetRoleIds are invalid.");

        RuleFor(x => x)
            .MustAsync(async (cmd, ct) =>
            {
                var total = await repository.GetTotalWeightByTypeExcludingAsync(
                    cmd.TypePerformanceMetricId, cmd.Id, ct);
                return total + cmd.Weight <= 100;
            })
            .WithMessage("Total weight of PerformanceMetrics within this type must not exceed 100.");
    }
}
