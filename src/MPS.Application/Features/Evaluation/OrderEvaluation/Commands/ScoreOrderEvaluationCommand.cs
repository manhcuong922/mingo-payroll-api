using MediatR;

namespace MPS.Application.Features.Evaluation.OrderEvaluation.Commands;

public record ScoreLineInput(int LineId, int Score, string? Note);

public record ScoreSubjectInput(int SubjectId, string? Note, IReadOnlyList<ScoreLineInput> Lines);

/// <summary>Tổ trưởng chấm điểm bảng-theo-đơn (Draft). Xem plan §4.2, §13.8.</summary>
public record ScoreOrderEvaluationCommand(
    int Id,
    string RowVersion,
    IReadOnlyList<ScoreSubjectInput> Subjects) : IRequest<bool>;
