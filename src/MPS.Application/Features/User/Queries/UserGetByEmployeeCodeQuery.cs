using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.User.Queries;

public record UserGetByEmployeeCodeQuery(string EmployeeCode) : IRequest<UserDto?>;
