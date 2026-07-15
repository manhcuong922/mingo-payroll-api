using MediatR;

namespace MPS.Application.Features.Employee.Dependent.Commands;

public sealed record ApproveDependentRegistrationRequestCommand(int Id, string? ReviewNote) : IRequest<bool>;
