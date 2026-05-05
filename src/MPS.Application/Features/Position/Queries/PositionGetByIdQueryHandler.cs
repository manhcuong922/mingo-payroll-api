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
public class PositionGetByIdQueryHandler : IRequestHandler<PositionGetByIdQuery, PositionDto?>
{
    private readonly IPositionRepository _repository;
    private readonly IMapper _mapper;

    public PositionGetByIdQueryHandler(IPositionRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PositionDto?> Handle(PositionGetByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return entity is null ? null : _mapper.Map<PositionDto>(entity);
    }
}
