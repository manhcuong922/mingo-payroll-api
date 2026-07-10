using AutoMapper;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Evaluation;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Queries;

public class GetMyMonthlyEvaluationsQueryHandler
    : IRequestHandler<GetMyMonthlyEvaluationsQuery, IReadOnlyList<MonthlyEvaluationDto>>
{
    private readonly IMonthlyEvaluationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public GetMyMonthlyEvaluationsQueryHandler(
        IMonthlyEvaluationRepository repo, ICurrentUserService currentUser, IMapper mapper)
    {
        _repo = repo;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<MonthlyEvaluationDto>> Handle(
        GetMyMonthlyEvaluationsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        var items = await _repo.GetForEmployeeAsync(userId, ct);
        return _mapper.Map<IReadOnlyList<MonthlyEvaluationDto>>(items);
    }
}
