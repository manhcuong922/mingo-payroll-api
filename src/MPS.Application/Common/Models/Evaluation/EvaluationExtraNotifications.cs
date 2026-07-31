using MediatR;

namespace MPS.Application.Common.Models.Evaluation;

/// <summary>Phase 6 — Đánh giá tháng công bố cho NV.</summary>
public sealed record MonthlyEvaluationPublishedNotification(
    int MonthlyEvaluationId,
    int EmployeeInformationId,
    int EmployeeUserId,
    int Year,
    int Month,
    decimal FinalScore) : INotification;
