using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.Department.Queries;

/// <param name="Keyword">Case+accent-insensitive LIKE trên Name / Code.</param>
public sealed record DepartmentGetAllQuery(
    bool IncludeInactive = false,
    string? Keyword = null)
    : IRequest<IReadOnlyList<DepartmentDto>>;
