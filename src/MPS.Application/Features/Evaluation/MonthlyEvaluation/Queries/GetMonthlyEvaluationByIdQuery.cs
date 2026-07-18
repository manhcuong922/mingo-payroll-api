using MediatR;
using MPS.Application.DTOs.Evaluation;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Queries;

public record GetMonthlyEvaluationByIdQuery(int Id) : IRequest<MonthlyEvaluationDto?>;
