namespace MPS.API.Requests.Evaluation;

public record CreateDirectMonthlyEvaluationRequest(int EmployeeUserId, int Year, int Month);

public record DirectScoreLineRequest(int LineId, int Score, string? Note);

public record ScoreDirectMonthlyEvaluationRequest(string RowVersion, IReadOnlyList<DirectScoreLineRequest> Lines);

public record ApproveMonthlyEvaluationRequest(string RowVersion);

public record ReopenMonthlyEvaluationRequest(string RowVersion, string Reason);
