using MediatR;
using MPS.Application.DTOs.Evaluation;
using MPS.Domain.Enums.Evaluation;

namespace MPS.Application.Features.Evaluation.OrderEvaluation.Queries;

/// <summary>Danh sách bảng-theo-đơn của tổ trưởng hiện tại (lọc theo trạng thái). Xem plan §13.10.</summary>
public record GetMyOrderEvaluationsQuery(EvaluationStatus? Status) : IRequest<IReadOnlyList<OrderEvaluationSummaryDto>>;
