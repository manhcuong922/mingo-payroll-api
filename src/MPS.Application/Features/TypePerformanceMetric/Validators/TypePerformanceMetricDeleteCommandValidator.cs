using FluentValidation;
using MPS.Application.Features.TypePerformanceMetric.Commands;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.TypePerformanceMetric.Validators;

public class TypePerformanceMetricDeleteCommandValidator
    : AbstractValidator<TypePerformanceMetricDeleteCommand>
{
    public TypePerformanceMetricDeleteCommandValidator(ITypePerformanceMetricRepository repository)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be greater than 0.")
            .MustAsync(async (id, ct) => !await repository.HasPerformanceMetricsAsync(id, ct))
            .WithMessage("Cannot delete a TypePerformanceMetric that still has associated PerformanceMetrics.");
    }
}
