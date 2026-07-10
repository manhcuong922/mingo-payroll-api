using MediatR;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Evaluation;
using MPS.Domain.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Evaluation;
using MPS.Domain.Enums.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Evaluation;

namespace MPS.Application.Features.Evaluation.OrderEvaluation.EventHandlers;

/// <summary>
/// Sinh bảng đánh giá theo đơn khi đơn chuyển Completed (D9). Idempotent + xử lý biên
/// E1 (không công nhân), E2 (thiếu tổ trưởng), E3 (chưa cấu hình tiêu chí), E8 (re-complete).
/// Xem plan §4.1, §13.7.
/// </summary>
public sealed class GenerateOrderEvaluationHandler : INotificationHandler<ProjectCompletedNotification>
{
    private readonly IProjectRepository _projects;
    private readonly IOrderEvaluationRepository _orderEvals;
    private readonly IEvaluationCriteriaSnapshotService _criteria;
    private readonly ILogger<GenerateOrderEvaluationHandler> _log;

    public GenerateOrderEvaluationHandler(
        IProjectRepository projects,
        IOrderEvaluationRepository orderEvals,
        IEvaluationCriteriaSnapshotService criteria,
        ILogger<GenerateOrderEvaluationHandler> log)
    {
        _projects = projects;
        _orderEvals = orderEvals;
        _criteria = criteria;
        _log = log;
    }

    public async Task Handle(ProjectCompletedNotification n, CancellationToken ct)
    {
        if (await _orderEvals.ExistsForProjectAsync(n.ProjectId, ct))
            return;   // E8 — idempotent (re-complete không nhân đôi)

        var project = await _projects.GetByIdAsync(n.ProjectId, ct);
        if (project is null)
        {
            _log.LogWarning("Không sinh bảng đánh giá: không tìm thấy đơn #{ProjectId}.", n.ProjectId);
            return;
        }

        if (project.ResponsibleUserId is null)
        {
            // E2 — đơn cũ trước refactor R/S chưa gán tổ trưởng. HR gán người chấm tay sau.
            _log.LogWarning(
                "Không sinh bảng đánh giá cho đơn #{ProjectId}: chưa gán tổ trưởng phụ trách (ResponsibleUserId=null).",
                n.ProjectId);
            return;
        }

        var employeeUserIds = await _orderEvals.GetSubjectUserIdsForProjectAsync(
            n.ProjectId, SystemRoles.EmployeeId, ct);
        if (employeeUserIds.Count == 0)
        {
            // E1 — đơn chỉ có container/gate, không công nhân trực tiếp.
            _log.LogInformation("Không sinh bảng đánh giá cho đơn #{ProjectId}: không có công nhân được giao.", n.ProjectId);
            return;
        }

        IReadOnlyList<EvaluationScoreLine> template;
        try
        {
            template = await _criteria.BuildLinesForRoleAsync(SystemRoles.EmployeeId, ct);
        }
        catch (DomainValidationException ex)
        {
            // E3 — chưa cấu hình tiêu chí cho role 7. Không tạo bảng rỗng vô nghĩa; cảnh báo HR.
            _log.LogWarning(ex,
                "Không sinh bảng đánh giá cho đơn #{ProjectId}: chưa cấu hình tiêu chí cho công nhân (role 7).",
                n.ProjectId);
            return;
        }

        var period = project.CompletedAt ?? VietnamClock.Now;

        var evaluation = new MPS.Domain.Entities.Evaluation.OrderEvaluation
        {
            ProjectId = project.Id,
            EvaluatorUserId = project.ResponsibleUserId.Value,
            PeriodYear = period.Year,
            PeriodMonth = period.Month,
            Status = EvaluationStatus.Draft,
            CreatedAt = VietnamClock.Now,
            Subjects = employeeUserIds
                .Select(uid => new OrderEvaluationSubject
                {
                    EmployeeUserId = uid,
                    SubjectRoleIdSnapshot = SystemRoles.EmployeeId,
                    FinalScore = null,
                    Scores = template.Select(CloneLine).ToList(),   // SNAPSHOT riêng cho từng NV (D2)
                })
                .ToList(),
        };

        await _orderEvals.AddAsync(evaluation, ct);

        _log.LogInformation(
            "Đã sinh bảng đánh giá theo đơn #{ProjectId} ({SubjectCount} công nhân, kỳ {Month}/{Year}).",
            project.Id, employeeUserIds.Count, period.Month, period.Year);
    }

    private static EvaluationScoreLine CloneLine(EvaluationScoreLine t) => new()
    {
        SourceMetricId = t.SourceMetricId,
        TypeNameSnapshot = t.TypeNameSnapshot,
        MetricNameSnapshot = t.MetricNameSnapshot,
        TypeWeightSnapshot = t.TypeWeightSnapshot,
        MetricWeightSnapshot = t.MetricWeightSnapshot,
        Scale = t.Scale,
        Score = null,
        Note = null,
    };
}
