using MediatR;

namespace MPS.Application.Features.Employee.Dependent.Commands;

public sealed record RejectDependentRegistrationRequestCommand(int Id, string? ReviewNote) : IRequest<bool>;
