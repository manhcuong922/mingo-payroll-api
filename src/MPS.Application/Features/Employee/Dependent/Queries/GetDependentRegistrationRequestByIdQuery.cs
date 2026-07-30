using MediatR;
using MPS.Application.DTOs.Employee;

namespace MPS.Application.Features.Employee.Dependent.Queries;

public sealed record GetDependentRegistrationRequestByIdQuery(int Id) : IRequest<DependentRegistrationRequestDto?>;
