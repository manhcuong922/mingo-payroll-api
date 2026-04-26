using MPS.Application.DTOs;
using MediatR;

namespace MPS.Application.Features.PerformanceMetric.Queries;

/// <summary>
/// Query lấy PerformanceMetric theo Id.
/// </summary>
public record PerformanceMetricGetByIdQuery(int Id) : IRequest<PerformanceMetricDto?>;
