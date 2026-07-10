using AutoMapper;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Evaluation;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Evaluation;

namespace MPS.Application.Features.Evaluation.OrderEvaluation.Queries;

public class GetOrderEvaluationByIdQueryHandler : IRequestHandler<GetOrderEvaluationByIdQuery, OrderEvaluationDto?>
{
    private readonly IOrderEvaluationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public GetOrderEvaluationByIdQueryHandler(
        IOrderEvaluationRepository repo, ICurrentUserService currentUser, IMapper mapper)
    {
        _repo = repo;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<OrderEvaluationDto?> Handle(GetOrderEvaluationByIdQuery request, CancellationToken ct)
    {
        var evaluation = await _repo.GetByIdWithGraphAsync(request.Id, ct);
        if (evaluation is null) return null;

        // ReadOwn: tổ trưởng chỉ xem bảng mình phụ trách; ReadAll (HR/Director) xem mọi bảng.
        if (!_currentUser.HasPermission(Permissions.OrderEvaluationReadAll)
            && evaluation.EvaluatorUserId != _currentUser.UserId)
            throw new ForbiddenAccessException("Bạn không phụ trách bảng đánh giá này.");

        return _mapper.Map<OrderEvaluationDto>(evaluation);
    }
}
