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
        var entity = new MPS.Domain.Entities.ProcessTemplate
        {
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            DefaultDepartmentId = request.DefaultDepartmentId,
            IsActive = true,
        };

        var seenOrders = new HashSet<int>();
        foreach (var s in request.Steps.OrderBy(s => s.StepOrder))
        {
            if (!seenOrders.Add(s.StepOrder))
                throw new DomainValidationException($"Trùng StepOrder {s.StepOrder} trong quy trình.");

            entity.Steps.Add(new ProcessStep
            {
                Name = s.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(s.Description) ? null : s.Description.Trim(),
                StepOrder = s.StepOrder,
                DefaultProgressType = s.DefaultProgressType,
                DefaultPlannedQuantity = s.DefaultPlannedQuantity,
                DefaultPlannedWorkUnits = s.DefaultPlannedWorkUnits,
                DefaultIsRequiredGate = s.DefaultIsRequiredGate,
                Priority = s.Priority,
            });
        }

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
