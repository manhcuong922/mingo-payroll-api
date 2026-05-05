using MediatR;
using MPS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.Position.Commands;
public class PositionUpdateCommandHandler : IRequestHandler<PositionUpdateCommand, bool>
{
    private readonly IPositionRepository _repository;

    public PositionUpdateCommandHandler(IPositionRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(PositionUpdateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.Position
        {
            Id = request.Id,
            Name = request.Name
        };

        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}