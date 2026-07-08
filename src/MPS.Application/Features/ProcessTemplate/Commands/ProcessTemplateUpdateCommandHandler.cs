using MediatR;
using MPS.Domain.Entities;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProcessTemplate.Commands;

public class ProcessTemplateUpdateCommandHandler : IRequestHandler<ProcessTemplateUpdateCommand, bool>
{
    private readonly IProcessTemplateRepository _repository;

    public ProcessTemplateUpdateCommandHandler(IProcessTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(ProcessTemplateUpdateCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdWithStepsTrackedAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy quy trình mẫu #{request.Id}.");

        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.IsActive = request.IsActive;
        entity.DefaultDepartmentId = request.DefaultDepartmentId;

        // Thay toàn bộ Steps (đơn giản, đúng semantics "sửa quy trình mẫu").
        entity.Steps.Clear();

        var seenOrders = new HashSet<int>();
        foreach (var s in request.Steps.OrderBy(s => s.StepOrder))
        {
            if (!seenOrders.Add(s.StepOrder))
                throw new DomainValidationException($"Trùng StepOrder {s.StepOrder} trong quy trình.");

            entity.Steps.Add(new ProcessStep
            {
                ProcessTemplateId = entity.Id,
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

        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}
