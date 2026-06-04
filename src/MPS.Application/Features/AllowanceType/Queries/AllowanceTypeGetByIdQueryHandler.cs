using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.AllowanceType.Queries;

public class AllowanceTypeGetByIdQueryHandler : IRequestHandler<AllowanceTypeGetByIdQuery, AllowanceTypeDto?>
{
    private readonly IAllowanceTypeRepository _repository;
    private readonly IMapper _mapper;

    public AllowanceTypeGetByIdQueryHandler(IAllowanceTypeRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<AllowanceTypeDto?> Handle(AllowanceTypeGetByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return entity is null ? null : _mapper.Map<AllowanceTypeDto>(entity);
    }
}
