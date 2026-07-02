using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;

namespace MPS.Application.Features.Department.Queries;

public sealed record DepartmentGetPagedQuery(
    string? Keyword, int Page, int PageSize, bool IncludeInactive = false)
    : IRequest<PagedResult<DepartmentDto>>;
