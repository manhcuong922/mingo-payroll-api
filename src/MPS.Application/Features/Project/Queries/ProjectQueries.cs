using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;
using MPS.Domain.Enums;

namespace MPS.Application.Features.Project.Queries;

public record ProjectGetAllQuery(bool IncludeCancelled = false, ProjectStatus? Status = null)
    : IRequest<IReadOnlyList<ProjectDto>>;

public record ProjectGetByIdQuery(int Id) : IRequest<ProjectDto?>;

public record ProjectGetPagedQuery(
    string? Keyword, int? DepartmentId, int Page, int PageSize, bool IncludeCancelled = false, ProjectStatus? Status = null)
    : IRequest<PagedResult<ProjectDto>>;
