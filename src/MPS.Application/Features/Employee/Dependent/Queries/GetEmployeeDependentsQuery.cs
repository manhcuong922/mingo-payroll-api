using MediatR;
using MPS.Application.DTOs.Employee;

namespace MPS.Application.Features.Employee.Dependent.Queries;

public sealed record GetEmployeeDependentsQuery(
    int EmployeeInformationId,
    bool OnlyActive = true) : IRequest<IReadOnlyList<EmployeeDependentDto>>;
