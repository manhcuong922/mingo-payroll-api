using FluentValidation;
using MPS.Application.Features.TypePerformanceMetric.Commands;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.TypePerformanceMetric.Validators;

public class TypePerformanceMetricCreateCommandValidator
    : AbstractValidator<TypePerformanceMetricCreateCommand>
{
    public TypePerformanceMetricCreateCommandValidator(ITypePerformanceMetricRepository repository)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Weight)
            .GreaterThan(0).WithMessage("Weight must be greater than 0.")
            .LessThanOrEqualTo(100).WithMessage("Weight must not exceed 100.");

        RuleFor(x => x)
            .MustAsync(async (cmd, ct) =>
            {
                var existingTotal = await repository.GetTotalWeightExcludingAsync(null, ct);
                return existingTotal + cmd.Weight <= 100;
            })
            .WithMessage("Total weight of all TypePerformanceMetrics must not exceed 100.");
    }
}
