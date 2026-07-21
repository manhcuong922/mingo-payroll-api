using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Attendance.AttendanceAdjustment.Commands;

public class ApproveAttendanceAdjustmentCommandHandler : IRequestHandler<ApproveAttendanceAdjustmentCommand, bool>
{
    private readonly IAttendanceAdjustmentRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPayrollSourceChangeHandler _payrollChange;

    public ApproveAttendanceAdjustmentCommandHandler(
        IAttendanceAdjustmentRepository repository,
        ICurrentUserService currentUser,
        IClock clock,
        IPayrollSourceChangeHandler payrollChange)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
        _payrollChange = payrollChange;
    }

    public async Task<bool> Handle(ApproveAttendanceAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        // Đọc trước để biết ngày công (đánh cờ đúng tháng) — GetByIdAsync đã Include AttendanceRecord.
        var adjustment = await _repository.GetByIdAsync(request.Id, cancellationToken);

        var applied = await _repository.ApproveAndApplyAsync(
            request.Id, userId, request.ReviewNote, _clock.UtcNow, cancellationToken);

        // BS-6 + BS-5: duyệt điều chỉnh công → mark stale kỳ Draft, capture Finalized cho NV.
        if (applied && adjustment?.AttendanceRecord is { } record)
            await _payrollChange.OnSourceChangedAsync(
                record.WorkDate.Year, record.WorkDate.Month, adjustment.EmployeeInformationId,
                $"Duyệt điều chỉnh công ngày {record.WorkDate:dd/MM/yyyy}", cancellationToken);

        return applied;
    }
}
