using MediatR;

namespace MPS.Application.Features.TypePerformanceMetric.Commands;

public record TypePerformanceMetricUpdateCommand(int Id, string Name, decimal Weight) : IRequest<bool>;
