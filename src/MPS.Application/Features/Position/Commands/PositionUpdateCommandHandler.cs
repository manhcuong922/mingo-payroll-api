using MediatR;
using MPS.Domain.Interfaces;

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
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
        };

        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}
