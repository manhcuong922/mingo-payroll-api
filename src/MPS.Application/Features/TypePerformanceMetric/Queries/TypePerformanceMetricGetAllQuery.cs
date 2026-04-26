using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.TypePerformanceMetric.Queries;

public record TypePerformanceMetricGetAllQuery : IRequest<IReadOnlyList<TypePerformanceMetricDto>>;
