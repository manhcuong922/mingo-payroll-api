using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.User.Commands;

public class UserUpdateCommandHandler : IRequestHandler<UserUpdateCommand, bool>
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRefreshTokenService _refreshTokenService;

    public UserUpdateCommandHandler(
        IUserRepository repository,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUserService,
        IRefreshTokenService refreshTokenService)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _currentUserService = currentUserService;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<bool> Handle(UserUpdateCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        var hasUpdateAll = _currentUserService.HasPermission(Permissions.UserUpdateAll);

        if (!hasUpdateAll)
        {
            if (_currentUserService.UserId != request.Id)
                throw new ForbiddenAccessException("You can only update your own user info.");

            if (request.RoleId != existing.RoleId)
                throw new ForbiddenAccessException("You cannot change your own role.");

            if (request.Status != existing.Status)
                throw new ForbiddenAccessException("You cannot change your own status.");
        }

        var statusChangedToDisabled = existing.Status == 1 && request.Status != 1;
        var roleChanged = existing.RoleId != request.RoleId;

        var entity = new Domain.Entities.User
        {
            Id = request.Id,
            EmployeeCode = request.EmployeeCode,
            FullName = request.FullName,
            RoleId = request.RoleId,
            Username = request.Username,
            PasswordHash = string.IsNullOrEmpty(request.Password)
                ? string.Empty
                : _passwordHasher.Hash(request.Password),
            Email = request.Email,
            Dob = request.Dob,
            PhoneNo = request.PhoneNo,
            Status = request.Status
        };

        var updated = await _repository.UpdateAsync(entity, cancellationToken);
        if (!updated) return false;

        if (statusChangedToDisabled || roleChanged)
        {
            var reason = statusChangedToDisabled ? "User disabled by admin" : "User role changed";
            await _refreshTokenService.RevokeAllForUserAsync(request.Id, reason, null, cancellationToken);
        }

        return true;
    }
}
