using AutoMapper;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Evaluation;

namespace MPS.Application.Features.Evaluation.OrderEvaluation.Queries;

public class GetMyOrderEvaluationsQueryHandler
    : IRequestHandler<GetMyOrderEvaluationsQuery, IReadOnlyList<OrderEvaluationSummaryDto>>
{
    private readonly IOrderEvaluationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public GetMyOrderEvaluationsQueryHandler(
        IOrderEvaluationRepository repo, ICurrentUserService currentUser, IMapper mapper)
    {
        _repo = repo;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<OrderEvaluationSummaryDto>> Handle(
        GetMyOrderEvaluationsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        var items = await _repo.GetForEvaluatorAsync(userId, request.Status, request.DepartmentId, ct);
        return _mapper.Map<IReadOnlyList<OrderEvaluationSummaryDto>>(items);
    }
}
