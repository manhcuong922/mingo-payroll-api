using MediatR;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Department.Commands;

public class DepartmentCreateCommandHandler : IRequestHandler<DepartmentCreateCommand, int>
{
    private readonly IDepartmentRepository _repository;

    public DepartmentCreateCommandHandler(IDepartmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<int> Handle(DepartmentCreateCommand request, CancellationToken cancellationToken)
    {
        var entity = new MPS.Domain.Entities.Department
        {
            Code        = request.Code.Trim().ToUpperInvariant(),
            Name        = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ManagerId   = request.ManagerId,
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
