using MediatR;
using MPS.Application.DTOs.Employee;
using MPS.Application.Features.Employee.Dependent.Common;
using MPS.Domain.Interfaces.Employee;

namespace MPS.Application.Features.Employee.Dependent.Queries;

public class GetDependentRegistrationRequestByIdQueryHandler : IRequestHandler<GetDependentRegistrationRequestByIdQuery, DependentRegistrationRequestDto?>
{
    private readonly IDependentRegistrationRequestRepository _repository;

    public GetDependentRegistrationRequestByIdQueryHandler(IDependentRegistrationRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<DependentRegistrationRequestDto?> Handle(GetDependentRegistrationRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var r = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return r is null ? null : DependentMapper.ToDto(r);
    }
}
