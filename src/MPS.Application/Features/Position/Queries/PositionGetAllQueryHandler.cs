using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.Position.Queries;
public class PositionGetAllQueryHandler : IRequestHandler<PositionGetAllQuery, IReadOnlyList<PositionDto>>
{
    private readonly IPositionRepository _repository;
    private readonly IMapper _mapper;

    public PositionGetAllQueryHandler(IPositionRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<PositionDto>> Handle(PositionGetAllQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<PositionDto>>(entities);
    }
}
