using MediatR;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Commands;

public record DirectScoreLineInput(int LineId, int Score, string? Note);

/// <summary>Director cho điểm bảng tháng Direct (Draft). Xem plan §4.4, §13.9.</summary>
public record ScoreDirectMonthlyEvaluationCommand(
    int Id,
    string RowVersion,
    IReadOnlyList<DirectScoreLineInput> Lines) : IRequest<bool>;
