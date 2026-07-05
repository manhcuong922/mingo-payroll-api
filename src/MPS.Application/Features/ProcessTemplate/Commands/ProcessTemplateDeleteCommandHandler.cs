using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProcessTemplate.Commands;

public class ProcessTemplateDeleteCommandHandler : IRequestHandler<ProcessTemplateDeleteCommand, bool>
{
    private readonly IProcessTemplateRepository _repository;

    public ProcessTemplateDeleteCommandHandler(IProcessTemplateRepository repository)
    {
        _repository = repository;
    }

    public Task<bool> Handle(ProcessTemplateDeleteCommand request, CancellationToken cancellationToken) =>
        _repository.DeleteAsync(request.Id, cancellationToken);
}
