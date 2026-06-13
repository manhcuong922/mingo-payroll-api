using AutoMapper;
using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.SalaryGrade.Queries;

public class SalaryGradeGetPagedQueryHandler : IRequestHandler<SalaryGradeGetPagedQuery, PagedResult<SalaryGradeDto>>
{
    private readonly ISalaryGradeRepository _repository;
    private readonly IMapper _mapper;

    public SalaryGradeGetPagedQueryHandler(ISalaryGradeRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PagedResult<SalaryGradeDto>> Handle(SalaryGradeGetPagedQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.PositionId, request.IsActive, request.Page, request.PageSize, cancellationToken);

        var dtos = _mapper.Map<IReadOnlyList<SalaryGradeDto>>(items);
        return new PagedResult<SalaryGradeDto>(dtos, total, request.Page, request.PageSize);
    }
}
