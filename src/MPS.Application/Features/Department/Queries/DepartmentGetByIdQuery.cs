using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.Department.Queries;

public sealed record DepartmentGetByIdQuery(int Id) : IRequest<DepartmentDto?>;
