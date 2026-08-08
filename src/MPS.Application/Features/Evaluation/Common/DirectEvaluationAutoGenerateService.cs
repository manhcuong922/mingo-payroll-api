using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Evaluation;
using MPS.Domain.Enums.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Evaluation;

namespace MPS.Application.Features.Evaluation.Common;

/// <inheritdoc cref="IDirectEvaluationAutoGenerateService"/>
public class DirectEvaluationAutoGenerateService : IDirectEvaluationAutoGenerateService
{
    private readonly IUserRepository _users;
    private readonly IMonthlyEvaluationRepository _monthly;
    private readonly IOrderEvaluationRepository _orderEvals;
    private readonly IEvaluationCriteriaSnapshotService _criteria;
    private readonly ILogger<DirectEvaluationAutoGenerateService> _log;

    public DirectEvaluationAutoGenerateService(
        IUserRepository users,
        IMonthlyEvaluationRepository monthly,
        IOrderEvaluationRepository orderEvals,
        IEvaluationCriteriaSnapshotService criteria,
        ILogger<DirectEvaluationAutoGenerateService> log)
    {
        _users = users;
        _monthly = monthly;
        _orderEvals = orderEvals;
        _criteria = criteria;
        _log = log;
    }

    public async Task<DirectEvaluationAutoGenerateResult> GenerateForMonthAsync(
        int year, int month, int actorUserId, CancellationToken ct)
    {
        var candidates = await _users.GetActiveByRoleIdsAsync(
            EvaluationSubjectRoles.DirectScorableRoleIds, ct);
        if (candidates.Count == 0)
            return new DirectEvaluationAutoGenerateResult(year, month, 0, 0, 0, 0);

        // Đã có bảng tháng (job chạy lại, hoặc Director đã tạo tay) → không đụng (idempotent).
        var alreadyHasMonthly = (await _monthly.GetExistingEmployeeUserIdsForMonthAsync(year, month, ct))
            .ToHashSet();

        // Kỳ này còn bảng-theo-đơn ⇒ vừa đổi vị trí giữa chừng: điểm tháng gộp từ đơn (Aggregated),
        // chấm trực tiếp áp dụng từ kỳ sau. Sinh Direct ở đây sẽ bị "Tổng hợp tháng" ghi đè.
        var scoredByOrder = (await _orderEvals.GetSubjectUserIdsByMonthAsync(year, month, ct))
            .ToHashSet();

        var targets = candidates
            .Where(u => !alreadyHasMonthly.Contains(u.Id) && !scoredByOrder.Contains(u.Id))
            .ToList();

        var skippedExisting = candidates.Count(u => alreadyHasMonthly.Contains(u.Id));
        var skippedOrderScored = candidates.Count(
            u => !alreadyHasMonthly.Contains(u.Id) && scoredByOrder.Contains(u.Id));

        if (targets.Count == 0)
            return new DirectEvaluationAutoGenerateResult(
                year, month, candidates.Count, 0, skippedExisting, skippedOrderScored);

        IReadOnlyList<EvaluationScoreLine> template;
        try
        {
            template = await _criteria.BuildLinesAsync(ct);
        }
        catch (DomainValidationException ex)
        {
            // E3 — rubric chưa sẵn sàng. Không sinh bảng rỗng/sai trọng số; báo để HR sửa rồi chạy lại.
            _log.LogWarning(ex,
                "Không auto-sinh bảng Direct kỳ {Month}/{Year}: rubric chưa sẵn sàng ({Reason}).",
                month, year, ex.Message);
            return new DirectEvaluationAutoGenerateResult(
                year, month, candidates.Count, 0, skippedExisting, skippedOrderScored, ex.Message);
        }

        var now = VietnamClock.Now;
        var entities = targets
            .Select(u => new MPS.Domain.Entities.Evaluation.MonthlyEvaluation
            {
                EmployeeUserId = u.Id,
                PeriodYear = year,
                PeriodMonth = month,
                SubjectRoleIdSnapshot = u.RoleId,
                Source = EvaluationSource.Direct,
                Status = EvaluationStatus.Draft,
                IsSystemDefault = true,
                CreatedByUserId = actorUserId,
                CreatedAt = now,
                Scores = template.Select(CloneWithDefaultScore).ToList(),   // SNAPSHOT riêng từng người (D2)
            })
            .ToList();

        // Rubric cân 100%/100% ⇒ mọi dòng = DefaultDirectScore thì FinalScore = chính điểm đó.
        // Vẫn chạy qua calculator để không có 2 nguồn sự thật về công thức.
        foreach (var e in entities)
            e.FinalScore = EvaluationScoreCalculator.ComputeFinalScore(e.Scores);

        var created = await _monthly.AddRangeAsync(entities, ct);

        _log.LogInformation(
            "Auto-sinh bảng Direct kỳ {Month}/{Year}: tạo {Created} bảng điểm mặc định {Score}/5 " +
            "(bỏ qua {SkippedExisting} đã có bảng, {SkippedOrder} còn chấm theo đơn).",
            month, year, created, EvaluationSubjectRoles.DefaultDirectScore,
            skippedExisting, skippedOrderScored);

        return new DirectEvaluationAutoGenerateResult(
            year, month, candidates.Count, created, skippedExisting, skippedOrderScored);
    }

    private static EvaluationScoreLine CloneWithDefaultScore(EvaluationScoreLine t) => new()
    {
        SourceMetricId = t.SourceMetricId,
        TypeNameSnapshot = t.TypeNameSnapshot,
        MetricNameSnapshot = t.MetricNameSnapshot,
        TypeWeightSnapshot = t.TypeWeightSnapshot,
        MetricWeightSnapshot = t.MetricWeightSnapshot,
        Scale = t.Scale,
        Score = EvaluationSubjectRoles.DefaultDirectScore,
        Note = null,
    };
}
