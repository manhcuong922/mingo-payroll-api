using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.SalaryGrade.Queries;

public class SalaryGradeGetByIdQueryHandler : IRequestHandler<SalaryGradeGetByIdQuery, SalaryGradeDto?>
{
    private readonly ISalaryGradeRepository _repository;
    private readonly IMapper _mapper;

    public SalaryGradeGetByIdQueryHandler(ISalaryGradeRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<SalaryGradeDto?> Handle(SalaryGradeGetByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return entity is null ? null : _mapper.Map<SalaryGradeDto>(entity);
    }
}
