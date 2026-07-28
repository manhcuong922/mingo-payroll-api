using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;
using MPS.Domain.Enums;

namespace MPS.Application.Features.Project.Queries;

public record ProjectGetAllQuery(bool IncludeCancelled = false, ProjectStatus? Status = null)
    : IRequest<IReadOnlyList<ProjectDto>>;

public record ProjectGetByIdQuery(int Id) : IRequest<ProjectDto?>;

/// <summary>Đơn hàng mà NV hiện tại có task được gán (kèm tiến độ) — dùng cho workspace công nhân.</summary>
public record ProjectGetMyQuery(bool IncludeCancelled = false) : IRequest<IReadOnlyList<ProjectDto>>;

public record ProjectGetPagedQuery(
    string? Keyword, int? DepartmentId, int Page, int PageSize, bool IncludeCancelled = false, ProjectStatus? Status = null)
    : IRequest<PagedResult<ProjectDto>>;
