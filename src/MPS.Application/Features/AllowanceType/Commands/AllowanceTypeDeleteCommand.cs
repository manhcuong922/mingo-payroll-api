using MediatR;

namespace MPS.Application.Features.AllowanceType.Commands;

public sealed record AllowanceTypeDeleteCommand(int Id) : IRequest<bool>;
