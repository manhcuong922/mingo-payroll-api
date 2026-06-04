using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.SalaryGrade.Queries;

public class SalaryGradeGetAllQueryHandler : IRequestHandler<SalaryGradeGetAllQuery, IReadOnlyList<SalaryGradeDto>>
{
    private readonly ISalaryGradeRepository _repository;
    private readonly IMapper _mapper;

    public SalaryGradeGetAllQueryHandler(ISalaryGradeRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<SalaryGradeDto>> Handle(SalaryGradeGetAllQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllAsync(request.PositionId, request.IsActive, cancellationToken);
        return _mapper.Map<IReadOnlyList<SalaryGradeDto>>(entities);
    }
}
