using MediatR;
using MPS.Application.DTOs;
using MPS.Application.Features.AuditLog.Queries;

namespace MPS.Application.Features.ProjectTask.Queries;

public record TaskGetByIdQuery(int Id) : IRequest<ProjectTaskDto?>;

public record TaskGetMyAssignedQuery : IRequest<IReadOnlyList<ProjectTaskDto>>;

public record TaskGetByDepartmentQuery(int DepartmentId) : IRequest<IReadOnlyList<ProjectTaskDto>>;

public record TaskGetByProjectQuery(int ProjectId) : IRequest<IReadOnlyList<ProjectTaskDto>>;

public record TaskAuditHistoryQuery(int TaskId) : IRequest<IReadOnlyList<AuditLogDto>>;
