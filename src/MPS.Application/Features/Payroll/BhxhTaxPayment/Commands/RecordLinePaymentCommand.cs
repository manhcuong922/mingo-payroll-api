using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Common;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Commands;

/// <summary>
/// Ghi nhận đã nộp cho 1 NV (line) trong obligation. Chỉ áp dụng khi line đang <c>Pending</c>
/// và obligation cha chưa <c>Completed</c>. Sau khi record: derive lại status cha
/// (Pending → PartiallyPaid nếu là line đầu tiên).
/// </summary>
public sealed record RecordLinePaymentCommand(
    int LineId,
    DateTime PaidDate,
    decimal PaidAmount,
    PaymentMethod PaymentMethod,
    string? TransactionRef,
    string? PaymentNotes,
    string RowVersion) : IRequest;

public sealed class RecordLinePaymentCommandHandler : IRequestHandler<RecordLinePaymentCommand>
{
    private readonly IPaymentObligationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public RecordLinePaymentCommandHandler(
        IPaymentObligationRepository repo, ICurrentUserService currentUser, IClock clock)
    {
        _repo = repo;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task Handle(RecordLinePaymentCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();

        var line = await _repo.GetLineByIdAsync(request.LineId, asNoTracking: false, ct)
            ?? throw new NotFoundException($"Không tìm thấy dòng nộp #{request.LineId}.");

        if (line.PaymentObligation.Status == PaymentObligationStatus.Completed)
            throw new ConflictException("Nghĩa vụ đã hoàn thành — không thể ghi nhận nộp thêm.");

        if (line.LineStatus != PaymentLineStatus.Pending)
            throw new ConflictException("Chỉ ghi nhận nộp được khi dòng đang ở trạng thái Chưa xử lý.");

        ApplyRowVersion(line, request.RowVersion);

        var now = _clock.UtcNow;
        line.LineStatus = PaymentLineStatus.Paid;
        line.PaidDate = request.PaidDate;
        line.PaidAmount = request.PaidAmount;
        line.PaymentMethod = request.PaymentMethod;
        line.TransactionRef = request.TransactionRef;
        line.PaymentNotes = request.PaymentNotes;
        line.RecordedByUserId = userId;
        line.RecordedAt = now;
        line.UpdatedAt = now;
        line.UpdatedByUserId = userId;

        await _repo.SaveChangesAsync(ct);

        await ParentStatusDeriver.RefreshAsync(_repo, line.PaymentObligation, userId, now, ct);
    }

    private static void ApplyRowVersion(Domain.Entities.Payroll.BhxhTax.PaymentObligationLine line, string rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
            throw new DomainValidationException("Thiếu rowVersion — vui lòng tải lại.");
        try
        {
            line.RowVersion = Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            throw new DomainValidationException("rowVersion không hợp lệ.");
        }
    }
}
