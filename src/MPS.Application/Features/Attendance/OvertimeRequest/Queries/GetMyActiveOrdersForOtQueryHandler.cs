using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

/// <summary>
/// Handler cho <see cref="GetMyActiveOrdersForOtQuery"/> — luồng OT v3 §5.1 (Q18).
///
/// Flow:
///  1. Resolve currentUser.
///  2. Distinct list ProjectId mà NV được assign task (không kiểm Status)
///     (repo: <see cref="IProjectTaskRepository.GetAssignedProjectIdsByUserAsync"/>).
///  3. Với mỗi ProjectId: load Project → chỉ giữ đơn Active, chưa deleted/completed.
///  4. Load task list assigned của NV trong đơn đó.
///  5. Enrich PM name (ResponsibleUser.FullName).
///  6. Trả list DTO — có thể ≥1 phần tử.
/// </summary>
public class GetMyActiveOrdersForOtQueryHandler
    : IRequestHandler<GetMyActiveOrdersForOtQuery, IReadOnlyList<MyActiveOrderForOtDto>>
{
    private readonly IProjectTaskRepository _taskRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUser;

    public GetMyActiveOrdersForOtQueryHandler(
        IProjectTaskRepository taskRepository,
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUser)
    {
        _taskRepository = taskRepository;
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MyActiveOrderForOtDto>> Handle(
        GetMyActiveOrdersForOtQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var projectIds = await _taskRepository.GetAssignedProjectIdsByUserAsync(userId, cancellationToken);
        if (projectIds.Count == 0) return Array.Empty<MyActiveOrderForOtDto>();

        var results = new List<MyActiveOrderForOtDto>(projectIds.Count);
        foreach (var projectId in projectIds)
        {
            var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);
            if (project is null) continue;
            if (project.DeletedAt is not null) continue;
            if (project.CompletedAt is not null) continue;
            if (project.Status != Domain.Enums.ProjectStatus.Active) continue;

            var tasks = await _taskRepository.GetAssignedTasksByUserInProjectAsync(
                userId, projectId, cancellationToken);
            if (tasks.Count == 0) continue;

            string? pmFullName = null;
            if (project.ResponsibleUserId is int pmUserId)
            {
                var pm = await _userRepository.GetByIdAsync(pmUserId, cancellationToken);
                pmFullName = pm?.FullName;
            }

            results.Add(new MyActiveOrderForOtDto(
                OrderId: project.Id,
                OrderTitle: project.Title,
                StartDate: project.StartDate,
                Deadline: project.Deadline,
                ResponsibleUserId: project.ResponsibleUserId,
                ResponsibleUserFullName: pmFullName,
                AssignedTasks: tasks.Select(t => new MyActiveOrderTaskDto(
                    TaskId: t.Id,
                    TaskTitle: t.Title,
                    Deadline: t.Deadline,
                    Priority: t.Priority)).ToList()));
        }

        return results;
    }
}
