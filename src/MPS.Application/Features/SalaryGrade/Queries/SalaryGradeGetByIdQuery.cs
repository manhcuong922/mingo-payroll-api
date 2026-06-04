using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.SalaryGrade.Queries;

public sealed record SalaryGradeGetByIdQuery(int Id) : IRequest<SalaryGradeDto?>;
