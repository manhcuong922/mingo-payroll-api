using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.Department.Queries;

public sealed record DepartmentGetAllQuery : IRequest<IReadOnlyList<DepartmentDto>>;
