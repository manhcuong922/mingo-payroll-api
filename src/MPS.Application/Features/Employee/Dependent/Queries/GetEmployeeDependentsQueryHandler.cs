using MediatR;
using MPS.Application.DTOs.Employee;
using MPS.Application.Features.Employee.Dependent.Common;
using MPS.Domain.Interfaces.Employee;

namespace MPS.Application.Features.Employee.Dependent.Queries;

public class GetEmployeeDependentsQueryHandler : IRequestHandler<GetEmployeeDependentsQuery, IReadOnlyList<EmployeeDependentDto>>
{
    private readonly IEmployeeDependentRepository _repository;

    public GetEmployeeDependentsQueryHandler(IEmployeeDependentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<EmployeeDependentDto>> Handle(GetEmployeeDependentsQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetByEmployeeAsync(request.EmployeeInformationId, request.OnlyActive, cancellationToken);
        return items.Select(DependentMapper.ToDto).ToList();
    }
}
