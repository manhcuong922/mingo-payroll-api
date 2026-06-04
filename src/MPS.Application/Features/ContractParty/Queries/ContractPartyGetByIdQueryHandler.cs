using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractParty.Queries;

public class ContractPartyGetByIdQueryHandler : IRequestHandler<ContractPartyGetByIdQuery, ContractPartyDto?>
{
    private readonly IContractPartyRepository _repository;
    private readonly IMapper _mapper;

    public ContractPartyGetByIdQueryHandler(IContractPartyRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<ContractPartyDto?> Handle(ContractPartyGetByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return entity is null ? null : _mapper.Map<ContractPartyDto>(entity);
    }
}
