using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Department.Commands;

public class DepartmentUpdateCommandHandler : IRequestHandler<DepartmentUpdateCommand, bool>
{
    private readonly IDepartmentRepository _repository;

    public DepartmentUpdateCommandHandler(IDepartmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DepartmentUpdateCommand request, CancellationToken cancellationToken)
    {
        var entity = new MPS.Domain.Entities.Department
        {
            Id          = request.Id,
            Code        = request.Code.Trim().ToUpperInvariant(),
            Name        = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ManagerId   = request.ManagerId,
        };

        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}
