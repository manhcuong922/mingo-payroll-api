using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Evaluation.Common;
using MPS.Domain.Exceptions;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Commands;

public class AggregateMonthlyEvaluationCommandHandler : IRequestHandler<AggregateMonthlyEvaluationCommand, int>
{
    private readonly IMonthlyAggregationService _aggregation;
    private readonly ICurrentUserService _currentUser;

    public AggregateMonthlyEvaluationCommandHandler(
        IMonthlyAggregationService aggregation, ICurrentUserService currentUser)
    {
        _aggregation = aggregation;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(AggregateMonthlyEvaluationCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        return await _aggregation.AggregateAsync(request.Year, request.Month, null, userId, ct);
    }
}
