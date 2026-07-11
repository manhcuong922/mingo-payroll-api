using MediatR;

namespace MPS.Application.Features.Employee.Dependent.Commands;

public sealed record CancelDependentRegistrationRequestCommand(int Id, string? CancelReason) : IRequest<bool>;
