using MediatR;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Commands;

/// <summary>HR duyệt + khóa bảng tháng (Draft → Approved) — chuẩn tính phụ cấp (D8, D11). Xem plan §4.5.</summary>
public record ApproveMonthlyEvaluationCommand(int Id, string RowVersion) : IRequest<bool>;
