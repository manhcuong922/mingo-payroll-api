using MediatR;

namespace MPS.Application.Features.PerformanceMetric.Commands;

public record PerformanceMetricUpdateCommand(
    int Id,
    string Name,
    string Description,
    decimal Weight,
    int TypePerformanceMetricId,
    IReadOnlyList<int> TargetRoleIds) : IRequest<bool>;
