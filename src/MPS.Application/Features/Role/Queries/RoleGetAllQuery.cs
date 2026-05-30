using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.Role.Queries;

public sealed record RoleGetAllQuery : IRequest<IReadOnlyList<RoleDto>>;
