using MediatR;

namespace MPS.Application.Features.User.Commands;

public record UserUpdateCommand(
    int Id,
    string EmployeeCode,
    string FullName,
    int RoleId,
    string Username,
    string? Password,
    string Email,
    DateTime Dob,
    string PhoneNo,
    int Status) : IRequest<bool>;
