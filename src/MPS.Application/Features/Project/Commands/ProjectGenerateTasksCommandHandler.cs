using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project;
using MPS.Application.Services;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Project.Commands;

public class ProjectGenerateTasksCommandHandler : IRequestHandler<ProjectGenerateTasksCommand, int>
{
    private readonly IProjectRepository _projects;
    private readonly IProcessTemplateRepository _templates;
    private readonly IProjectTaskRepository _tasks;
    private readonly ITaskGenerator _generator;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public ProjectGenerateTasksCommandHandler(
        IProjectRepository projects,
        IProcessTemplateRepository templates,
        IProjectTaskRepository tasks,
        ITaskGenerator generator,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _projects = projects;
        _templates = templates;
        _tasks = tasks;
        _generator = generator;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(ProjectGenerateTasksCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        var project = await _projects.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy đơn #{request.ProjectId}.");

        // Scope v5: PM chỉ áp quy trình cho đơn xưởng mình; GĐ mọi xưởng.
        await ProjectScopeGuard.EnsureCanWriteAsync(project.DepartmentId, _currentUser, _authorizer, cancellationToken);

        var template = await _templates.GetByIdWithStepsAsync(request.ProcessTemplateId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy quy trình mẫu #{request.ProcessTemplateId}.");

        var stages = _generator.BuildStages(project, template, userId, request.Overrides);

        await _tasks.AddRangeAsync(stages, cancellationToken);
        return stages.Count;
    }
}
