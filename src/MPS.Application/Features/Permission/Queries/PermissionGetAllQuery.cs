using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.Permission.Queries;

public sealed record PermissionGetAllQuery : IRequest<IReadOnlyList<PermissionDto>>;
