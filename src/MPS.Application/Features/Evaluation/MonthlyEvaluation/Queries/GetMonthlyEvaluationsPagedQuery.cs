using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Evaluation;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Queries;

/// <summary>HR/Director xem bảng tháng theo kỳ (+ lọc phòng ban). Xem plan §13.10.</summary>
public record GetMonthlyEvaluationsPagedQuery(
    int Year, int Month, int? DepartmentId, int Page, int PageSize)
    : IRequest<PagedResult<MonthlyEvaluationDto>>;
