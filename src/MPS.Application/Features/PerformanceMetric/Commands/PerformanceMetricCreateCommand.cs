using MediatR;

namespace MPS.Application.Features.PerformanceMetric.Commands;

public record PerformanceMetricCreateCommand(
    string Name,
    string Description,
    decimal Weight,
    int TypePerformanceMetricId,
    IReadOnlyList<int> TargetRoleIds) : IRequest<int>;
