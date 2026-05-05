using MediatR;
using MPS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.Position.Commands;
public class PositionDeleteCommandHandler : IRequestHandler<PositionDeleteCommand, bool>
{
    private readonly IPositionRepository _repository;

    public PositionDeleteCommandHandler(IPositionRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(PositionDeleteCommand request, CancellationToken cancellationToken)
    {
        return await _repository.DeleteAsync(request.Id, cancellationToken);
    }
}