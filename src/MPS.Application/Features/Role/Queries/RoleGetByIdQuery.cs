using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.Role.Queries;

public sealed record RoleGetByIdQuery(int Id) : IRequest<RoleDetailDto?>;
