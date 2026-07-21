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
    private readonly IPayrollStaleMarker _payrollStale;

    public ApproveAttendanceAdjustmentCommandHandler(
        IAttendanceAdjustmentRepository repository,
        ICurrentUserService currentUser,
        IClock clock,
        IPayrollStaleMarker payrollStale)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
        _payrollStale = payrollStale;
    }

    public async Task<bool> Handle(ApproveAttendanceAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        // Đọc trước để biết ngày công (đánh cờ đúng tháng) — GetByIdAsync đã Include AttendanceRecord.
        var adjustment = await _repository.GetByIdAsync(request.Id, cancellationToken);

        var applied = await _repository.ApproveAndApplyAsync(
            request.Id, userId, request.ReviewNote, _clock.UtcNow, cancellationToken);

        // BS-6: duyệt điều chỉnh công → kỳ lương Draft của tháng đó cần tính lại.
        if (applied && adjustment?.AttendanceRecord is { } record)
            await _payrollStale.MarkStaleForMonthAsync(
                record.WorkDate.Year, record.WorkDate.Month,
                $"Duyệt điều chỉnh công ngày {record.WorkDate:dd/MM/yyyy}", cancellationToken);

        return applied;
    }
}
