using MediatR;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Evaluation.Common;
using MPS.Domain.Common;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Evaluation;

namespace MPS.Application.Features.Evaluation.OrderEvaluation.Commands;

public class SubmitOrderEvaluationCommandHandler : IRequestHandler<SubmitOrderEvaluationCommand, bool>
{
    private readonly IOrderEvaluationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IMonthlyAggregationService _aggregation;
    private readonly ILogger<SubmitOrderEvaluationCommandHandler> _log;

    public SubmitOrderEvaluationCommandHandler(
        IOrderEvaluationRepository repo,
        ICurrentUserService currentUser,
        IMonthlyAggregationService aggregation,
        ILogger<SubmitOrderEvaluationCommandHandler> log)
    {
        _repo = repo;
        _currentUser = currentUser;
        _aggregation = aggregation;
        _log = log;
    }

    public async Task<bool> Handle(SubmitOrderEvaluationCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        var evaluation = await _repo.GetByIdForUpdateAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy bảng đánh giá theo đơn #{request.Id}.");

        if (evaluation.EvaluatorUserId != userId && !_currentUser.HasPermission(Permissions.OrderEvaluationReadAll))
            throw new ForbiddenAccessException("Bạn không phụ trách bảng đánh giá này.");

        if (evaluation.Status != EvaluationStatus.Draft)
            throw new DomainValidationException("Bảng đã được chốt trước đó.");

        // D10/E4 — đơn qua tháng chưa chấm xong thì KHÔNG submit được (còn tiêu chí điểm null).
        var hasUnscored = evaluation.Subjects.Count == 0
            || evaluation.Subjects.Any(s => s.Scores.Count == 0 || s.Scores.Any(l => l.Score == null));
        if (hasUnscored)
            throw new DomainValidationException("Còn nhân viên/tiêu chí chưa chấm đủ điểm, không thể chốt bảng.");

        foreach (var subject in evaluation.Subjects)
            subject.FinalScore = EvaluationScoreCalculator.ComputeFinalScore(subject.Scores);

        evaluation.Status = EvaluationStatus.Submitted;
        evaluation.SubmittedAt = VietnamClock.Now;
        evaluation.SubmittedByUserId = userId;

        var expected = RowVersionCodec.Decode(request.RowVersion);
        var ok = await _repo.UpdateWithConcurrencyAsync(evaluation, expected, ct);
        if (!ok)
            throw new ConflictException("Dữ liệu đã thay đổi ở nơi khác. Vui lòng tải lại rồi thử lại.");

        // D15 — auto-recalc bảng tháng còn Draft cho kỳ này (chốt là hành vi chính; gộp là phụ).
        try
        {
            await _aggregation.AggregateAsync(evaluation.PeriodYear, evaluation.PeriodMonth, null, userId, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "Lỗi auto-tổng hợp bảng tháng sau khi chốt bảng-theo-đơn #{Id} (bảng đã chốt thành công; HR có thể tổng hợp tay).",
                evaluation.Id);
        }

        return true;
    }
}
