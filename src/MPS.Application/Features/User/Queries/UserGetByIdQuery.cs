using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.User.Queries;

public record UserGetByIdQuery(int Id) : IRequest<UserDto?>;
