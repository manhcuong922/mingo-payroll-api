using MediatR;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Department.Commands;

public class DepartmentDeleteCommandHandler : IRequestHandler<DepartmentDeleteCommand, bool>
{
    private readonly IDepartmentRepository _repository;

    public DepartmentDeleteCommandHandler(IDepartmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DepartmentDeleteCommand request, CancellationToken cancellationToken)
    {
        if (await _repository.HasEmployeesAsync(request.Id, cancellationToken))
            throw new DomainValidationException("Cannot delete a department that still has employees.");

        return await _repository.DeleteAsync(request.Id, cancellationToken);
    }
}
