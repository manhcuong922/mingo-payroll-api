using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Commands;

/// <summary>
/// Ghi nhận đã nộp cho 1 nghĩa vụ BHXH/TNCN. Chỉ áp dụng khi nghĩa vụ đang Pending hoặc Overdue.
/// Nếu <see cref="PaidAmount"/> khớp AmountDue ⇒ status <c>PaidConfirmed</c>; chênh ⇒ <c>PaidWithDiscrepancy</c>
/// (chờ user gọi tiếp <see cref="ConfirmPaymentDiscrepancyCommand"/> để đóng bản ghi).
/// Evidence ảnh (bank receipt + authority confirmation) được upload qua endpoint riêng
/// <c>UploadObligationEvidenceCommand</c> — không nhét vào lệnh này để tách concern.
/// </summary>
public sealed record RecordPaymentCommand(
    int ObligationId,
    DateTime PaidDate,
    decimal PaidAmount,
    PaymentMethod PaymentMethod,
    string? TransactionRef,
    string? Notes) : IRequest;

public sealed class RecordPaymentCommandHandler : IRequestHandler<RecordPaymentCommand>
{
    private readonly IPaymentObligationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public RecordPaymentCommandHandler(
        IPaymentObligationRepository repo,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repo = repo;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task Handle(RecordPaymentCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();
        var obligation = await _repo.GetByIdAsync(request.ObligationId, asNoTracking: false, ct)
            ?? throw new NotFoundException($"Không tìm thấy nghĩa vụ nộp #{request.ObligationId}.");

        if (obligation.Status is not (PaymentObligationStatus.Pending or PaymentObligationStatus.Overdue))
            throw new ConflictException(
                "Chỉ ghi nhận nộp được khi nghĩa vụ đang ở trạng thái Chưa nộp hoặc Quá hạn.");

        var now = _clock.UtcNow;
        obligation.PaidDate = request.PaidDate;
        obligation.PaidAmount = request.PaidAmount;
        obligation.PaymentMethod = request.PaymentMethod;
        obligation.TransactionRef = request.TransactionRef;
        obligation.Notes = request.Notes;
        obligation.RecordedByUserId = userId;
        obligation.RecordedAt = now;
        obligation.UpdatedAt = now;
        obligation.UpdatedByUserId = userId;

        obligation.Status = request.PaidAmount == obligation.AmountDue
            ? PaymentObligationStatus.PaidConfirmed
            : PaymentObligationStatus.PaidWithDiscrepancy;

        // Ghi nhận khớp → clear cờ confirmed cũ (trường hợp trước đó đã có, giờ record lại đúng số).
        if (obligation.Status == PaymentObligationStatus.PaidConfirmed)
        {
            obligation.ConfirmedByUserId = null;
            obligation.ConfirmedAt = null;
        }

        await _repo.SaveChangesAsync(ct);
    }
}
