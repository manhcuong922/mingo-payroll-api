using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Employee;
using MPS.Application.Features.Employee.Dependent.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Employee;

namespace MPS.Application.Features.Employee.Dependent.Queries;

public class GetMyDependentsQueryHandler : IRequestHandler<GetMyDependentsQuery, IReadOnlyList<EmployeeDependentDto>>
{
    private readonly IEmployeeDependentRepository _repository;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;

    public GetMyDependentsQueryHandler(
        IEmployeeDependentRepository repository,
        IContractLookupReader lookup,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _lookup = lookup;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<EmployeeDependentDto>> Handle(GetMyDependentsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        var items = await _repository.GetByEmployeeAsync(employeeId, request.OnlyActive, cancellationToken);
        return items.Select(DependentMapper.ToDto).ToList();
    }
}
