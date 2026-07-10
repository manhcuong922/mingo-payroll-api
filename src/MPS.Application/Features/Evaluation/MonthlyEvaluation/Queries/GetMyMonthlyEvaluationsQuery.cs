using MediatR;
using MPS.Application.DTOs.Evaluation;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Queries;

/// <summary>NV xem điểm tháng của chính mình (Read.Self). Xem plan §13.10.</summary>
public record GetMyMonthlyEvaluationsQuery() : IRequest<IReadOnlyList<MonthlyEvaluationDto>>;
