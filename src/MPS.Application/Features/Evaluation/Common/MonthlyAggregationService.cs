using MPS.Domain.Interfaces.Evaluation;

namespace MPS.Application.Features.Evaluation.Common;

public class MonthlyAggregationService : IMonthlyAggregationService
{
    private readonly IOrderEvaluationRepository _orderEvals;
    private readonly IMonthlyEvaluationRepository _monthly;

    public MonthlyAggregationService(
        IOrderEvaluationRepository orderEvals,
        IMonthlyEvaluationRepository monthly)
    {
        _orderEvals = orderEvals;
        _monthly = monthly;
    }

    public async Task<int> AggregateAsync(
        int year, int month, int? onlyEmployeeUserId, int actorUserId, CancellationToken ct)
    {
        var subjects = await _orderEvals.GetSubmittedSubjectsByMonthAsync(year, month, onlyEmployeeUserId, ct);
        if (subjects.Count == 0) return 0;

        var updated = 0;
        foreach (var group in subjects.GroupBy(s => s.EmployeeUserId))
        {
            var contributions = group
                .Where(s => s.FinalScore.HasValue)
                .Select(s => new AggregatedContribution(s.Id, s.FinalScore!.Value))
                .ToList();
            if (contributions.Count == 0) continue;

            var subjectRole = group.First().SubjectRoleIdSnapshot;
            // Trung bình cộng đơn giản — mỗi đơn trọng số bằng nhau (D7, §5.2).
            var finalScore = Math.Round(contributions.Average(c => c.Score), 2, MidpointRounding.AwayFromZero);
            var outcome = await _monthly.UpsertAggregatedAsync(
                group.Key, year, month, subjectRole, actorUserId, finalScore, contributions, ct);

            if (outcome != MonthlyAggregateOutcome.SkippedApproved)
                updated++;
        }

        return updated;
    }
}
