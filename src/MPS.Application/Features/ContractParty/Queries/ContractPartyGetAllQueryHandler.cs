using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractParty.Queries;

public class ContractPartyGetAllQueryHandler : IRequestHandler<ContractPartyGetAllQuery, IReadOnlyList<ContractPartyDto>>
{
    private readonly IContractPartyRepository _repository;
    private readonly IMapper _mapper;

    public ContractPartyGetAllQueryHandler(IContractPartyRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ContractPartyDto>> Handle(ContractPartyGetAllQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllAsync(request.ActiveOnly, cancellationToken);
        return _mapper.Map<IReadOnlyList<ContractPartyDto>>(entities);
    }
}
