using MediatR;

namespace MPS.Application.Features.Department.Commands;

public sealed record DepartmentCreateCommand(
    string Code,
    string Name,
    string? Description,
    int? ManagerId) : IRequest<int>;
