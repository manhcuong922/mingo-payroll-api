using MediatR;
using MPS.Domain.Entities;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProcessTemplate.Commands;

public class ProcessTemplateCreateCommandHandler : IRequestHandler<ProcessTemplateCreateCommand, int>
{
    private readonly IProcessTemplateRepository _repository;

    public ProcessTemplateCreateCommandHandler(IProcessTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<int> Handle(ProcessTemplateCreateCommand request, CancellationToken cancellationToken)
    {
        if (request.BaseQuantity <= 0)
            throw new DomainValidationException("BaseQuantity phải lớn hơn 0.");

        var entity = new MPS.Domain.Entities.ProcessTemplate
        {
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            BaseQuantity = request.BaseQuantity,
            DefaultDepartmentId = request.DefaultDepartmentId,
            IsActive = true,
            Steps = ProcessTemplateTreeBuilder.Build(request.Steps),
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
