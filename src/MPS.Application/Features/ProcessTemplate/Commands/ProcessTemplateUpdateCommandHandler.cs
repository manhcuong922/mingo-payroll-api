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

        if (request.BaseQuantity <= 0)
            throw new DomainValidationException("BaseQuantity phải lớn hơn 0.");

        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.IsActive = request.IsActive;
        entity.BaseQuantity = request.BaseQuantity;
        entity.DefaultDepartmentId = request.DefaultDepartmentId;

        // Thay toàn bộ cây node (đơn giản, đúng semantics "sửa quy trình mẫu"). Xóa con trước cha (self-ref).
        entity.Steps.Clear();
        foreach (var node in ProcessTemplateTreeBuilder.Build(request.Steps))
            entity.Steps.Add(node);

        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}
