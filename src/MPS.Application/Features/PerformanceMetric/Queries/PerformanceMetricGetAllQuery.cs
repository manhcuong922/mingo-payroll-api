using MPS.Application.DTOs;
using MediatR;

namespace MPS.Application.Features.PerformanceMetric.Queries;

/// <summary>
/// Query lấy danh sách toàn bộ PerformanceMetric.
/// </summary>
public record PerformanceMetricGetAllQuery : IRequest<IReadOnlyList<PerformanceMetricDto>>;
