using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;
using MPS.Domain.Enums;

namespace MPS.Application.Features.Project.Queries;

public record ProjectGetAllQuery(bool IncludeCancelled = false, ProjectStatus? Status = null)
    : IRequest<IReadOnlyList<ProjectDto>>;

public record ProjectGetByIdQuery(int Id) : IRequest<ProjectDto?>;

/// <summary>
/// Đơn hàng liên quan trực tiếp tới NV hiện tại — kèm tiến độ trọn cây. Dùng cho workspace công nhân
/// và tổ trưởng sản xuất. Scope (plan §2.2): P xuất hiện ⇔ P.ResponsibleUserId == userId
/// HOẶC ∃ Task T ∈ P.Tasks: T.Assignees ∋ userId. Xem docs/plans/team-leader-orders-api.md.
/// </summary>
public record ProjectGetMyQuery(bool IncludeCancelled = false) : IRequest<IReadOnlyList<ProjectDto>>;

public record ProjectGetPagedQuery(
    string? Keyword, int? DepartmentId, int Page, int PageSize, bool IncludeCancelled = false, ProjectStatus? Status = null)
    : IRequest<PagedResult<ProjectDto>>;
