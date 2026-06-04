using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.AllowanceType.Queries;

public class AllowanceTypeGetAllQueryHandler : IRequestHandler<AllowanceTypeGetAllQuery, IReadOnlyList<AllowanceTypeDto>>
{
    private readonly IAllowanceTypeRepository _repository;
    private readonly IMapper _mapper;

    public AllowanceTypeGetAllQueryHandler(IAllowanceTypeRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<AllowanceTypeDto>> Handle(AllowanceTypeGetAllQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllAsync(request.IsActive, cancellationToken);
        return _mapper.Map<IReadOnlyList<AllowanceTypeDto>>(entities);
    }
}
