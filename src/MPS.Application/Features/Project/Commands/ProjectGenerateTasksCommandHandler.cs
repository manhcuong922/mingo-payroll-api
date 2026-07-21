using MediatR;
using MPS.Application.Common.Interfaces;
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

        // Lập kế hoạch: Planning Manager của xưởng đơn / GĐ.
        await ProjectScopeGuard.EnsureCanWriteAsync(project.DepartmentId, _currentUser, _authorizer, cancellationToken);

        // Fail-fast trước khi vào khoá (đường thường, tránh chi phí transaction cho request lặp).
        if (await _tasks.HasAnyByProjectAsync(request.ProjectId, cancellationToken))
            throw new DomainValidationException("Đơn đã có task — không thể sinh lại. Xoá cây task cũ trước nếu muốn đổi quy trình.");

        var template = await _templates.GetByIdWithStepsAsync(request.ProcessTemplateId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy quy trình mẫu #{request.ProcessTemplateId}.");

        // ⭐ #16: idempotency thật sự nằm ở đây — check-then-insert được bọc trong transaction + app-lock
        // theo project, nên 2 request song song không thể cùng sinh cây task (trước đây ra 2 cây chồng nhau).
        var generated = await _tasks.GenerateExclusiveAsync(
            request.ProjectId,
            _ => Task.FromResult<IReadOnlyList<Domain.Entities.ProjectTask>?>(
                _generator.BuildTree(project, template, userId)),
            cancellationToken);

        return generated
            ?? throw new DomainValidationException(
                "Đơn đã có task — không thể sinh lại. Xoá cây task cũ trước nếu muốn đổi quy trình.");
    }
}
