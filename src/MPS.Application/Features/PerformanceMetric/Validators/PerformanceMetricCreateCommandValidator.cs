using MPS.Application.Features.PerformanceMetric.Commands;
using FluentValidation;

namespace MPS.Application.Features.PerformanceMetric.Validators;

/// <summary>
/// Validator cho PerformanceMetricCreateCommand.
/// </summary>
public class PerformanceMetricCreateCommandValidator : AbstractValidator<PerformanceMetricCreateCommand>
{
    public PerformanceMetricCreateCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");

        RuleFor(x => x.Weight)
            .InclusiveBetween(0, 100).WithMessage("Weight must be between 0 and 100.");

        RuleFor(x => x.TargetRoleIds)
            .NotNull().WithMessage("TargetRoleIds is required.");
    }
}
