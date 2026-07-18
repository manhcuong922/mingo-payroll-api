using MediatR;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Commands;

/// <summary>HR mở lại bảng tháng đã khóa (Approved → Draft), giữ lịch sử qua AuditLog. Xem plan §4.5.</summary>
public record ReopenMonthlyEvaluationCommand(int Id, string RowVersion, string Reason) : IRequest<bool>;
