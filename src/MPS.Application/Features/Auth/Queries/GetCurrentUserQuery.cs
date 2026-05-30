using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.Auth.Queries;

public sealed record GetCurrentUserQuery : IRequest<CurrentUserDto?>;
