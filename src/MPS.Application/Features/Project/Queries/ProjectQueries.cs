using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;

namespace MPS.Application.Features.Project.Queries;

public record ProjectGetAllQuery(bool IncludeCancelled = false) : IRequest<IReadOnlyList<ProjectDto>>;

public record ProjectGetByIdQuery(int Id) : IRequest<ProjectDto?>;

public record ProjectGetPagedQuery(string? Keyword, int? DepartmentId, int Page, int PageSize, bool IncludeCancelled = false)
    : IRequest<PagedResult<ProjectDto>>;
