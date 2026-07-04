using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Project.Commands;

public class ProjectDeleteCommandHandler : IRequestHandler<ProjectDeleteCommand, bool>
{
    private readonly IProjectRepository _repository;

    public ProjectDeleteCommandHandler(IProjectRepository repository)
    {
        _repository = repository;
    }

    public Task<bool> Handle(ProjectDeleteCommand request, CancellationToken cancellationToken) =>
        _repository.DeleteAsync(request.Id, cancellationToken);
}
