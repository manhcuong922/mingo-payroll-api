using MediatR;

namespace MPS.Application.Features.TypePerformanceMetric.Commands;

public record TypePerformanceMetricDeleteCommand(int Id) : IRequest<bool>;
