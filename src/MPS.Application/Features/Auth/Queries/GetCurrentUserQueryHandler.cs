using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Auth.Queries;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, CurrentUserDto?>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;

    public GetCurrentUserQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
    }

    public async Task<CurrentUserDto?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
            return null;

        var user = await _userRepository.GetByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        if (user is null) return null;

        return new CurrentUserDto(
            Id: user.Id,
            EmployeeCode: user.EmployeeCode,
            FullName: user.FullName,
            RoleId: user.RoleId,
            RoleName: user.Role?.Name ?? string.Empty,
            Username: user.Username,
            Email: user.Email,
            Dob: user.Dob,
            PhoneNo: user.PhoneNo,
            Status: user.Status,
            LastLoginAt: user.LastLoginAt,
            Permissions: _currentUserService.Permissions);
    }
}
