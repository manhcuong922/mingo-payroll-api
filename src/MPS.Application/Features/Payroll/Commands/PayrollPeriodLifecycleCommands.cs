using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.Commands;

// ===== Chốt kỳ (Draft → Finalized) =====
public sealed record FinalizePayrollPeriodCommand(int PeriodId) : IRequest;

public sealed class FinalizePayrollPeriodCommandHandler : IRequestHandler<FinalizePayrollPeriodCommand>
{
    private readonly IPayrollPeriodRepository _periods;
    private readonly ICurrentUserService _currentUser;

    public FinalizePayrollPeriodCommandHandler(IPayrollPeriodRepository periods, ICurrentUserService currentUser)
    {
        _periods = periods;
        _currentUser = currentUser;
    }

    public async Task Handle(FinalizePayrollPeriodCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();
        var period = await _periods.GetByIdAsync(request.PeriodId, asNoTracking: false, ct)
            ?? throw new NotFoundException($"Không tìm thấy kỳ lương #{request.PeriodId}.");
        if (period.Status != PayrollPeriodStatus.Draft)
            throw new ConflictException("Chỉ chốt được kỳ đang ở trạng thái Nháp.");

        period.Status = PayrollPeriodStatus.Finalized;
        period.UpdatedAt = DateTime.UtcNow;
        period.UpdatedByUserId = userId;
        await _periods.SaveChangesAsync(ct);
    }
}

// ===== Xoá kỳ (chỉ Draft) =====
public sealed record DeletePayrollPeriodCommand(int PeriodId) : IRequest;

public sealed class DeletePayrollPeriodCommandHandler : IRequestHandler<DeletePayrollPeriodCommand>
{
    private readonly IPayrollPeriodRepository _periods;
    public DeletePayrollPeriodCommandHandler(IPayrollPeriodRepository periods) => _periods = periods;

    public async Task Handle(DeletePayrollPeriodCommand request, CancellationToken ct)
    {
        var period = await _periods.GetByIdAsync(request.PeriodId, asNoTracking: true, ct)
            ?? throw new NotFoundException($"Không tìm thấy kỳ lương #{request.PeriodId}.");
        if (period.Status != PayrollPeriodStatus.Draft)
            throw new ConflictException("Chỉ xoá được kỳ đang ở trạng thái Nháp.");
        await _periods.DeleteAsync(request.PeriodId, ct);
    }
}
