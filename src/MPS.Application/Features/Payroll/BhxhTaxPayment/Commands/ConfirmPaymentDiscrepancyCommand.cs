using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Commands;

/// <summary>
/// User xác nhận chấp nhận số tiền chênh lệch giữa PaidAmount và AmountDue.
/// Chỉ áp dụng khi nghĩa vụ ở trạng thái <see cref="PaymentObligationStatus.PaidWithDiscrepancy"/>.
/// Sau khi confirm ⇒ status <see cref="PaymentObligationStatus.PaidConfirmed"/>, đóng bản ghi.
/// </summary>
public sealed record ConfirmPaymentDiscrepancyCommand(int ObligationId, string? ConfirmationNote) : IRequest;

public sealed class ConfirmPaymentDiscrepancyCommandHandler : IRequestHandler<ConfirmPaymentDiscrepancyCommand>
{
    private readonly IPaymentObligationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public ConfirmPaymentDiscrepancyCommandHandler(
        IPaymentObligationRepository repo,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repo = repo;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task Handle(ConfirmPaymentDiscrepancyCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();
        var obligation = await _repo.GetByIdAsync(request.ObligationId, asNoTracking: false, ct)
            ?? throw new NotFoundException($"Không tìm thấy nghĩa vụ nộp #{request.ObligationId}.");

        if (obligation.Status != PaymentObligationStatus.PaidWithDiscrepancy)
            throw new ConflictException("Chỉ xác nhận được khi nghĩa vụ đang ở trạng thái Đã nộp có chênh lệch.");

        var now = _clock.UtcNow;
        obligation.Status = PaymentObligationStatus.PaidConfirmed;
        obligation.ConfirmedByUserId = userId;
        obligation.ConfirmedAt = now;
        obligation.UpdatedAt = now;
        obligation.UpdatedByUserId = userId;

        if (!string.IsNullOrWhiteSpace(request.ConfirmationNote))
        {
            var oldNotes = string.IsNullOrWhiteSpace(obligation.Notes) ? string.Empty : obligation.Notes + "\n";
            obligation.Notes = oldNotes + $"[Xác nhận chênh lệch @ {now:yyyy-MM-dd}] {request.ConfirmationNote}";
        }

        await _repo.SaveChangesAsync(ct);
    }
}
