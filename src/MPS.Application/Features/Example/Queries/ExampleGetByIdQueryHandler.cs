using MPS.Application.DTOs;
using MPS.Domain.Interfaces;
using MediatR;

namespace MPS.Application.Features.Example.Queries;

/// <summary>
/// Ví dụ Query Handler - xử lý logic đọc dữ liệu.
/// </summary>
public class ExampleGetByIdQueryHandler : IRequestHandler<ExampleGetByIdQuery, ExampleDto?>
{
    private readonly IExampleRepository _repository;

    public ExampleGetByIdQueryHandler(IExampleRepository repository)
    {
        _repository = repository;
    }

    public async Task<ExampleDto?> Handle(ExampleGetByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity is null) return null;

        return new ExampleDto(entity.Id, entity.Name, entity.CreatedAt);
    }
}
