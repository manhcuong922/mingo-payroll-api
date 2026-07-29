using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Common;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Commands;

/// <summary>
/// Undo 1 line từ Paid|Excluded về Pending — dùng khi lỡ nhập nhầm. Chỉ áp dụng khi obligation
/// cha chưa <c>Completed</c>. Clear toàn bộ payment/exclusion fields.
/// </summary>
public sealed record RevertLineCommand(int LineId, string RowVersion) : IRequest;

public sealed class RevertLineCommandHandler : IRequestHandler<RevertLineCommand>
{
    private readonly IPaymentObligationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public RevertLineCommandHandler(
        IPaymentObligationRepository repo, ICurrentUserService currentUser, IClock clock)
    {
        _repo = repo;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task Handle(RevertLineCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();

        var line = await _repo.GetLineByIdAsync(request.LineId, asNoTracking: false, ct)
            ?? throw new NotFoundException($"Không tìm thấy dòng nộp #{request.LineId}.");

        if (line.PaymentObligation.Status == PaymentObligationStatus.Completed)
            throw new ConflictException("Nghĩa vụ đã hoàn thành — không thể hoàn tác dòng.");

        if (line.LineStatus == PaymentLineStatus.Pending)
            throw new ConflictException("Dòng đã ở trạng thái Chưa xử lý — không cần hoàn tác.");

        try { line.RowVersion = Convert.FromBase64String(request.RowVersion); }
        catch (FormatException) { throw new DomainValidationException("rowVersion không hợp lệ."); }

        var now = _clock.UtcNow;
        line.LineStatus = PaymentLineStatus.Pending;
        line.PaidDate = null;
        line.PaidAmount = null;
        line.PaymentMethod = null;
        line.TransactionRef = null;
        line.PaymentNotes = null;
        line.RecordedByUserId = null;
        line.RecordedAt = null;
        line.ExcludedReason = null;
        line.ExcludedByUserId = null;
        line.ExcludedAt = null;
        line.UpdatedAt = now;
        line.UpdatedByUserId = userId;

        await _repo.SaveChangesAsync(ct);
        await ParentStatusDeriver.RefreshAsync(_repo, line.PaymentObligation, userId, now, ct);
    }
}
