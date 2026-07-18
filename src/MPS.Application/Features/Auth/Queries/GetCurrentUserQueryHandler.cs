using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Auth.Queries;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, CurrentUserDto?>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;

    public GetCurrentUserQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IEmployeeInformationRepository employeeRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<CurrentUserDto?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
            return null;

        var user = await _userRepository.GetByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        if (user is null) return null;

        // Join EmployeeInformation theo UserId để lấy Gender. Nullable — user hệ thống
        // (SystemAdmin, ai-service...) không có EmployeeInformation gắn kèm.
        var employee = await _employeeRepository.GetByUserIdAsync(user.Id, cancellationToken);

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
            Gender: employee?.Gender,
            Status: user.Status,
            LastLoginAt: user.LastLoginAt,
            Permissions: _currentUserService.Permissions);
    }
}
