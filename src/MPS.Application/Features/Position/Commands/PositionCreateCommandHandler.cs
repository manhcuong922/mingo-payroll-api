using MediatR;
using MPS.Domain.Interfaces;

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
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim()
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
