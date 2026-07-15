using MediatR;
using MPS.Application.DTOs.Evaluation;

namespace MPS.Application.Features.Evaluation.OrderEvaluation.Queries;

public record GetOrderEvaluationByIdQuery(int Id) : IRequest<OrderEvaluationDto?>;
