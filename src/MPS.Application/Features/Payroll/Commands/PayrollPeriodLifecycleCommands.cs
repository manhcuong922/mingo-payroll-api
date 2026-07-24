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
    private readonly IPayrollAdjustmentRepository _adjustments;

    public DeletePayrollPeriodCommandHandler(
        IPayrollPeriodRepository periods, IPayrollAdjustmentRepository adjustments)
    {
        _periods = periods;
        _adjustments = adjustments;
    }

    public async Task Handle(DeletePayrollPeriodCommand request, CancellationToken ct)
    {
        var period = await _periods.GetByIdAsync(request.PeriodId, asNoTracking: true, ct)
            ?? throw new NotFoundException($"Không tìm thấy kỳ lương #{request.PeriodId}.");
        if (period.Status != PayrollPeriodStatus.Draft)
            throw new ConflictException("Chỉ xoá được kỳ đang ở trạng thái Nháp.");

        // BS-5: trả các khoản truy lĩnh/truy thu đã áp vào kỳ này về trạng thái CHƯA áp — nếu không,
        // xoá kỳ vừa vi phạm FK (Restrict) vừa làm mất khoản (không kỳ nào gom lại nữa).
        var applied = await _adjustments.GetAppliedToPeriodAsync(request.PeriodId, ct);
        foreach (var adjustment in applied)
        {
            adjustment.AppliedPeriodId = null;
            adjustment.AppliedAt = null;
        }

        // #44: khoản thiếu do SÀN 0 của chính kỳ này sinh ra chỉ tồn tại vì kỳ này tồn tại. Xoá kỳ
        // ⇒ phải xoá luôn, nếu không (a) vi phạm FK `SourcePeriodId` (Restrict) và (b) nhân viên bị
        // thu một khoản mà kỳ lương gốc đã không còn.
        var sourced = await _adjustments.GetPendingSourcedByPeriodAsync(request.PeriodId, ct);
        foreach (var adjustment in sourced) _adjustments.Remove(adjustment);

        if (applied.Count > 0 || sourced.Count > 0) await _adjustments.SaveChangesAsync(ct);

        await _periods.DeleteAsync(request.PeriodId, ct);
    }
}
