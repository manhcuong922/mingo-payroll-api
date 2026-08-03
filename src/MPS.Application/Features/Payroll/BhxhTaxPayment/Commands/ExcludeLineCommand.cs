using MediatR;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Payroll;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Common;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Commands;

/// <summary>
/// Bỏ qua 1 NV khỏi lô nộp — VD: NV nghỉ việc trước hạn, đã đóng ở nơi khác, tranh chấp.
/// Chỉ áp dụng khi line đang <c>Pending</c> và obligation cha chưa <c>Completed</c>.
/// <see cref="ExcludedReason"/> bắt buộc để audit.
/// </summary>
public sealed record ExcludeLineCommand(
    int LineId,
    string ExcludedReason,
    string RowVersion) : IRequest;

public sealed class ExcludeLineCommandHandler : IRequestHandler<ExcludeLineCommand>
{
    private readonly IPaymentObligationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IMediator _mediator;
    private readonly ILogger<ExcludeLineCommandHandler> _logger;

    public ExcludeLineCommandHandler(
        IPaymentObligationRepository repo,
        ICurrentUserService currentUser,
        IClock clock,
        IMediator mediator,
        ILogger<ExcludeLineCommandHandler> logger)
    {
        _repo = repo;
        _currentUser = currentUser;
        _clock = clock;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(ExcludeLineCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();

        var line = await _repo.GetLineByIdAsync(request.LineId, asNoTracking: false, ct)
            ?? throw new NotFoundException($"Không tìm thấy dòng nộp #{request.LineId}.");

        if (line.PaymentObligation.Status == PaymentObligationStatus.Completed)
            throw new ConflictException("Nghĩa vụ đã hoàn thành — không thể bỏ qua dòng thêm.");

        if (line.LineStatus != PaymentLineStatus.Pending)
            throw new ConflictException("Chỉ bỏ qua được khi dòng đang ở trạng thái Chưa xử lý.");

        try { line.RowVersion = Convert.FromBase64String(request.RowVersion); }
        catch (FormatException) { throw new DomainValidationException("rowVersion không hợp lệ."); }

        var now = _clock.UtcNow;
        line.LineStatus = PaymentLineStatus.Excluded;
        line.ExcludedReason = request.ExcludedReason.Trim();
        line.ExcludedByUserId = userId;
        line.ExcludedAt = now;
        line.UpdatedAt = now;
        line.UpdatedByUserId = userId;

        await _repo.SaveChangesAsync(ct);
        await ParentStatusDeriver.RefreshAsync(_repo, line.PaymentObligation, userId, now, ct);

        // v3.1 — thông báo NV chủ line. Publish lỗi không rollback nghiệp vụ chính.
        try
        {
            var period = line.PaymentObligation.PayrollPeriod;
            var notification = new BhxhTaxLineExcludedNotification(
                LineId: line.Id,
                ObligationId: line.PaymentObligationId,
                ObligationType: line.PaymentObligation.ObligationType,
                PeriodYear: period.PeriodYear,
                PeriodMonth: period.PeriodMonth,
                EmployeeInformationId: line.EmployeeInformationId,
                EmployeeUserId: line.EmployeeInformation.UserId,
                EmployeeCodeSnapshot: line.EmployeeCodeSnapshot,
                EmployeeFullNameSnapshot: line.EmployeeFullNameSnapshot,
                ExcludedReason: line.ExcludedReason ?? string.Empty,
                ExcludedAt: line.ExcludedAt ?? now);
            await _mediator.Publish(notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Publish BhxhTaxLineExcludedNotification thất bại — LineId={LineId}, ObligationId={ObligationId}",
                line.Id, line.PaymentObligationId);
        }
    }
}
