using MediatR;

namespace MPS.Application.Features.Evaluation.OrderEvaluation.Commands;

/// <summary>Tổ trưởng chốt bảng-theo-đơn (Draft → Submitted). Xem plan §4.2, §13.9.</summary>
public record SubmitOrderEvaluationCommand(int Id, string RowVersion) : IRequest<bool>;
