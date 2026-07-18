using MediatR;
using MPS.Application.DTOs;
using MPS.Application.Features.AuditLog.Queries;

namespace MPS.Application.Features.ProjectTask.Queries;

public record TaskGetByIdQuery(int Id) : IRequest<ProjectTaskDto?>;

public record TaskGetMyAssignedQuery(bool IncludeCancelled = false) : IRequest<IReadOnlyList<ProjectTaskDto>>;

public record TaskGetByDepartmentQuery(int DepartmentId, bool IncludeCancelled = false) : IRequest<IReadOnlyList<ProjectTaskDto>>;

public record TaskGetByProjectQuery(int ProjectId, bool IncludeCancelled = false) : IRequest<IReadOnlyList<ProjectTaskDto>>;

public record TaskAuditHistoryQuery(int TaskId) : IRequest<IReadOnlyList<AuditLogDto>>;
