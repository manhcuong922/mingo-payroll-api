using AutoMapper;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Evaluation;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Evaluation;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Queries;

public class GetMonthlyEvaluationByIdQueryHandler
    : IRequestHandler<GetMonthlyEvaluationByIdQuery, MonthlyEvaluationDto?>
{
    private readonly IMonthlyEvaluationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public GetMonthlyEvaluationByIdQueryHandler(
        IMonthlyEvaluationRepository repo, ICurrentUserService currentUser, IMapper mapper)
    {
        _repo = repo;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<MonthlyEvaluationDto?> Handle(GetMonthlyEvaluationByIdQuery request, CancellationToken ct)
    {
        var evaluation = await _repo.GetByIdWithGraphAsync(request.Id, ct);
        if (evaluation is null) return null;

        // ReadAll (HR/Director) xem mọi bảng; NV chỉ xem bảng của chính mình (Read.Self).
        if (!_currentUser.HasPermission(Permissions.MonthlyEvaluationReadAll)
            && evaluation.EmployeeUserId != _currentUser.UserId)
            throw new ForbiddenAccessException("Bạn chỉ được xem bảng đánh giá của chính mình.");

        return _mapper.Map<MonthlyEvaluationDto>(evaluation);
    }
}
