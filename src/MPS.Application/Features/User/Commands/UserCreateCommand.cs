using MediatR;

namespace MPS.Application.Features.User.Commands;

public record UserCreateCommand(
    int EmployeeInformationId,
    int RoleId) : IRequest<UserCreateResult>;

public sealed record UserCreateResult(int UserId, string EmployeeCode, string Username);
