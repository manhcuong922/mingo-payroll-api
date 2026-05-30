using MediatR;
using MPS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.Position.Commands;
public class PositionCreateCommandHandler : IRequestHandler<PositionCreateCommand, int>
{
    private readonly IPositionRepository _repository;

    public PositionCreateCommandHandler(IPositionRepository repository)
    {
        _repository = repository;
    }

    public async Task<int> Handle(PositionCreateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.Position
        {
            Name = request.Name
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}