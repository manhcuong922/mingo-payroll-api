using MediatR;

namespace MPS.Application.Features.Department.Commands;

public sealed record DepartmentDeleteCommand(int Id) : IRequest<bool>;
