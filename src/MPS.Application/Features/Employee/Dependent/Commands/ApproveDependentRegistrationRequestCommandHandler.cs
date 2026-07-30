using MediatR;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Employee;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Employee.Dependent.Commands;

public class ApproveDependentRegistrationRequestCommandHandler : IRequestHandler<ApproveDependentRegistrationRequestCommand, bool>
{
    private readonly IDependentRegistrationRequestRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPayrollStaleMarker _payrollStale;
    private readonly ILogger<ApproveDependentRegistrationRequestCommandHandler> _logger;

    public ApproveDependentRegistrationRequestCommandHandler(
        IDependentRegistrationRequestRepository repository,
        ICurrentUserService currentUser,
        IClock clock,
        IPayrollStaleMarker payrollStale,
        ILogger<ApproveDependentRegistrationRequestCommandHandler> logger)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
        _payrollStale = payrollStale;
        _logger = logger;
    }

    public async Task<bool> Handle(ApproveDependentRegistrationRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        // HR chuyên trách duyệt — role phải là HR (2).
        if (_currentUser.RoleId != SystemRoles.HrId && _currentUser.RoleId != SystemRoles.SystemAdminId)
            throw new ForbiddenAccessException("Chỉ HR/Admin được duyệt đơn đăng ký người phụ thuộc.");

        var applied = await _repository.ApproveAndApplyAsync(
            request.Id, userId, request.ReviewNote, _clock.UtcNow, cancellationToken);

        if (applied)
            await TryMarkPayrollStaleAsync(request.Id, cancellationToken);

        return applied;
    }

    // #66/[D27] — NPT chảy vào SL_NPT → GT_NPT → THUE_TNCN. Duyệt/huỷ một suất hiệu lực trong tháng
    // N ⇒ mọi kỳ Draft từ tháng N trở đi phải bật cờ "cần tính lại". Best-effort (side-effect phụ).
    private async Task TryMarkPayrollStaleAsync(int requestId, CancellationToken ct)
    {
        try
        {
            var req = await _repository.GetByIdAsync(requestId, ct);
            var effective = req?.RegisteredFromDate ?? req?.RegisteredToDate;
            if (req is null || effective is null) return;

            await _payrollStale.MarkStaleFromMonthAsync(
                effective.Value.Year, effective.Value.Month,
                $"Duyệt đơn người phụ thuộc #{requestId} (hiệu lực {effective.Value:dd/MM/yyyy})", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Không đánh được cờ stale sau khi duyệt đơn NPT #{RequestId}; việc duyệt vẫn thành công.",
                requestId);
        }
    }
}
