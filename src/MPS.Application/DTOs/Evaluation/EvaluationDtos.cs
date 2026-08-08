namespace MPS.Application.DTOs.Evaluation;

/// <summary>1 dòng điểm tiêu chí (snapshot D2).</summary>
public record EvaluationScoreLineDto(
    int Id,
    int? SourceMetricId,
    string TypeNameSnapshot,
    string MetricNameSnapshot,
    decimal TypeWeightSnapshot,
    decimal MetricWeightSnapshot,
    int Scale,
    int? Score,
    string? Note);

/// <summary>1 công nhân trong bảng-theo-đơn.</summary>
public record OrderEvaluationSubjectDto(
    int Id,
    int EmployeeUserId,
    string? EmployeeName,
    int SubjectRoleIdSnapshot,
    decimal? FinalScore,
    string? Note,
    IReadOnlyList<EvaluationScoreLineDto> Scores);

/// <summary>Bảng đánh giá theo đơn (chi tiết). <see cref="RowVersion"/> base64 — client gửi lại khi ghi.</summary>
public record OrderEvaluationDto(
    int Id,
    int ProjectId,
    string? ProjectTitle,
    int EvaluatorUserId,
    string? EvaluatorName,
    int PeriodYear,
    int PeriodMonth,
    string Status,
    DateTime? SubmittedAt,
    DateTime CreatedAt,
    string RowVersion,
    IReadOnlyList<OrderEvaluationSubjectDto> Subjects);

/// <summary>Bảng-theo-đơn (tóm tắt cho danh sách "chờ chấm" của tổ trưởng).</summary>
public record OrderEvaluationSummaryDto(
    int Id,
    int ProjectId,
    string? ProjectTitle,
    int PeriodYear,
    int PeriodMonth,
    string Status,
    int SubjectCount,
    int ScoredSubjectCount);

/// <summary>Trace 1 bảng-theo-đơn góp vào bảng tháng (Aggregated).</summary>
public record MonthlyEvaluationSourceLinkDto(
    int Id,
    int OrderEvaluationSubjectId,
    decimal ContributedScore);

/// <summary>Bảng tháng chuẩn. <see cref="RowVersion"/> base64 — client gửi lại khi ghi.</summary>
public record MonthlyEvaluationDto(
    int Id,
    int EmployeeUserId,
    string? EmployeeName,
    int PeriodYear,
    int PeriodMonth,
    int SubjectRoleIdSnapshot,
    string Source,
    decimal? FinalScore,
    string Status,
    bool IsSystemDefault,
    int? ApprovedByUserId,
    DateTime? ApprovedAt,
    int? ReopenedByUserId,
    DateTime? ReopenedAt,
    string? ReopenReason,
    int CreatedByUserId,
    DateTime CreatedAt,
    string RowVersion,
    IReadOnlyList<MonthlyEvaluationSourceLinkDto> SourceLinks,
    IReadOnlyList<EvaluationScoreLineDto> Scores);
