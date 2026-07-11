using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Employee;
using MPS.Domain.Enums.Employee;

namespace MPS.Application.Features.Employee.Dependent.Queries;

public sealed record GetDependentRegistrationRequestsQuery(
    int? EmployeeInformationId,
    DependentRequestStatus? Status,
    int Page = 1,
    int Size = 20) : IRequest<PagedResult<DependentRegistrationRequestDto>>;
