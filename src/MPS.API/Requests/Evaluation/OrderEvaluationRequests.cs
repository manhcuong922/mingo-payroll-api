namespace MPS.API.Requests.Evaluation;

public record ScoreLineRequest(int LineId, int Score, string? Note);

public record ScoreSubjectRequest(int SubjectId, string? Note, IReadOnlyList<ScoreLineRequest> Lines);

public record ScoreOrderEvaluationRequest(string RowVersion, IReadOnlyList<ScoreSubjectRequest> Subjects);

public record SubmitOrderEvaluationRequest(string RowVersion);
