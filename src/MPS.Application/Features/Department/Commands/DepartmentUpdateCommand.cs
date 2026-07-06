using MediatR;

namespace MPS.Application.Features.Department.Commands;

public sealed record DepartmentUpdateCommand(
    int Id,
    string Code,
    string Name,
    string? Description,
    int? ManagerId) : IRequest<bool>;
