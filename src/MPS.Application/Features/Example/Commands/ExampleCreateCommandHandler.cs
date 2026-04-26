using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using MediatR;

namespace MPS.Application.Features.Example.Commands;

/// <summary>
/// Ví dụ Command Handler - xử lý logic nghiệp vụ cho Command.
/// </summary>
public class ExampleCreateCommandHandler : IRequestHandler<ExampleCreateCommand, Guid>
{
    private readonly IExampleRepository _repository;

    public ExampleCreateCommandHandler(IExampleRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(ExampleCreateCommand request, CancellationToken cancellationToken)
    {
        var entity = new ExampleEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            CreatedAt = DateTime.UtcNow
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
