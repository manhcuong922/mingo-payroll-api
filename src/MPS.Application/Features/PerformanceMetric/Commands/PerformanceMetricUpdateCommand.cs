using MediatR;

namespace MPS.Application.Features.PerformanceMetric.Commands;

/// <summary>
/// Command cập nhật PerformanceMetric.
/// </summary>
public record PerformanceMetricUpdateCommand(
    int Id,
    string Name,
    string Description,
    decimal Weight,
    IReadOnlyList<int> TargetRoleIds) : IRequest<bool>;
