using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Commands;

/// <summary>
/// User bấm nút "Hoàn thành" cho 1 nghĩa vụ. Guard cứng:
/// - Obligation chưa <c>Completed</c>.
/// - Mọi line ∈ {Paid, Excluded} (PendingCount == 0 và TotalLinesCount &gt; 0).
/// - Có ≥1 evidence.
///
/// Set <c>Status = Completed</c>, <c>CompletedAt</c>, <c>CompletedByUserId</c>. Sau khi Completed:
/// mọi mutation line/evidence bị block.
/// </summary>
public sealed record CompleteObligationCommand(int ObligationId) : IRequest;

public sealed class CompleteObligationCommandHandler : IRequestHandler<CompleteObligationCommand>
{
    private readonly IPaymentObligationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CompleteObligationCommandHandler(
        IPaymentObligationRepository repo, ICurrentUserService currentUser, IClock clock)
    {
        _repo = repo;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task Handle(CompleteObligationCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();

        var obligation = await _repo.GetByIdAsync(request.ObligationId, asNoTracking: false, ct)
            ?? throw new NotFoundException($"Không tìm thấy nghĩa vụ nộp #{request.ObligationId}.");

        if (obligation.Status == PaymentObligationStatus.Completed)
            throw new ConflictException("Nghĩa vụ đã hoàn thành trước đó.");

        var counts = await _repo.CountLinesByStatusAsync(obligation.Id, ct);
        if (counts.Total == 0)
            throw new ConflictException("Nghĩa vụ không có dòng nào để hoàn thành.");
        if (!counts.AllHandled)
            throw new DomainValidationException(
                $"Còn {counts.Pending} nhân viên chưa được ghi nhận nộp hoặc bỏ qua.");

        var evidenceCount = await _repo.CountEvidenceAsync(obligation.Id, ct);
        if (evidenceCount == 0)
            throw new DomainValidationException("Cần tải lên ít nhất 1 ảnh chứng từ trước khi hoàn thành.");

        var now = _clock.UtcNow;
        obligation.Status = PaymentObligationStatus.Completed;
        obligation.CompletedAt = now;
        obligation.CompletedByUserId = userId;
        obligation.UpdatedAt = now;
        obligation.UpdatedByUserId = userId;

        await _repo.SaveChangesAsync(ct);
    }
}
