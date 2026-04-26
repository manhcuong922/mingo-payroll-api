using MediatR;

namespace MPS.Application.Features.TypePerformanceMetric.Commands;

public record TypePerformanceMetricCreateCommand(string Name, decimal Weight) : IRequest<int>;
