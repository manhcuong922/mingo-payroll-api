using AutoMapper;
using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Evaluation;
using MPS.Domain.Interfaces.Evaluation;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Queries;

public class GetMonthlyEvaluationsPagedQueryHandler
    : IRequestHandler<GetMonthlyEvaluationsPagedQuery, PagedResult<MonthlyEvaluationDto>>
{
    private readonly IMonthlyEvaluationRepository _repo;
    private readonly IMapper _mapper;

    public GetMonthlyEvaluationsPagedQueryHandler(IMonthlyEvaluationRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<PagedResult<MonthlyEvaluationDto>> Handle(
        GetMonthlyEvaluationsPagedQuery request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var size = request.PageSize is < 1 or > 200 ? 20 : request.PageSize;

        var (items, total) = await _repo.GetPagedByMonthAsync(
            request.Year, request.Month, request.DepartmentId, page, size, ct);

        var dtos = _mapper.Map<IReadOnlyList<MonthlyEvaluationDto>>(items);
        return new PagedResult<MonthlyEvaluationDto>(dtos, total, page, size);
    }
}
