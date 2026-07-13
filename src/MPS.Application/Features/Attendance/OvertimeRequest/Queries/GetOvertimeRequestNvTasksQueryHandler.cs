using MediatR;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

public class GetOvertimeRequestNvTasksQueryHandler
    : IRequestHandler<GetOvertimeRequestNvTasksQuery, IReadOnlyList<MyActiveOrderTaskDto>>
{
    private readonly IOvertimeRequestRepository _repository;
    private readonly IProjectTaskRepository _taskRepository;

    public GetOvertimeRequestNvTasksQueryHandler(
        IOvertimeRequestRepository repository,
        IProjectTaskRepository taskRepository)
    {
        _repository = repository;
        _taskRepository = taskRepository;
    }

    public async Task<IReadOnlyList<MyActiveOrderTaskDto>> Handle(
        GetOvertimeRequestNvTasksQuery query, CancellationToken cancellationToken)
    {
        var request = await _repository.GetByIdAsync(query.RequestId, cancellationToken)
            ?? throw new NotFoundException($"Overtime request {query.RequestId} not found.");

        if (request.ProjectId is not int projectId)
            return Array.Empty<MyActiveOrderTaskDto>();

        // NV = người tạo request (v3 luồng NV self-create). Query tasks NV được assign
        // trong đơn của request đó — không lọc Status task (khớp Q18).
        var tasks = await _taskRepository.GetAssignedTasksByUserInProjectAsync(
            request.CreatedByUserId, projectId, cancellationToken);

        return tasks.Select(t => new MyActiveOrderTaskDto(
            TaskId: t.Id,
            TaskTitle: t.Title,
            Deadline: t.Deadline,
            Priority: t.Priority)).ToList();
    }
}
