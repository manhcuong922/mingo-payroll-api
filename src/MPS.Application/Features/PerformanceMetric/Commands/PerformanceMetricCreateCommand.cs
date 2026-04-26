using MediatR;

namespace MPS.Application.Features.PerformanceMetric.Commands;

/// <summary>
/// Command tạo mới PerformanceMetric.
/// </summary>
public record PerformanceMetricCreateCommand(
    string Name,
    string Description,
    decimal Weight,
    IReadOnlyList<int> TargetRoleIds) : IRequest<int>;
