using MediatR;

namespace MPS.Application.Features.PerformanceMetric.Commands;

/// <summary>
/// Command xóa PerformanceMetric theo Id.
/// </summary>
public record PerformanceMetricDeleteCommand(int Id) : IRequest<bool>;
