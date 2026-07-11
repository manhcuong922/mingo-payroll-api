using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Employee;

namespace MPS.Application.Features.Employee.Dependent.Commands;

public class ApproveDependentRegistrationRequestCommandHandler : IRequestHandler<ApproveDependentRegistrationRequestCommand, bool>
{
    private readonly IDependentRegistrationRequestRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public ApproveDependentRegistrationRequestCommandHandler(
        IDependentRegistrationRequestRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(ApproveDependentRegistrationRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        // HR chuyên trách duyệt — role phải là HR (2).
        if (_currentUser.RoleId != SystemRoles.HrId && _currentUser.RoleId != SystemRoles.SystemAdminId)
            throw new ForbiddenAccessException("Chỉ HR/Admin được duyệt đơn đăng ký người phụ thuộc.");

        return await _repository.ApproveAndApplyAsync(
            request.Id, userId, request.ReviewNote, _clock.UtcNow, cancellationToken);
    }
}
