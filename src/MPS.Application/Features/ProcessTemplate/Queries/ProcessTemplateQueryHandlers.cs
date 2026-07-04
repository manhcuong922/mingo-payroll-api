using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProcessTemplate.Queries;

public class ProcessTemplateGetAllQueryHandler
    : IRequestHandler<ProcessTemplateGetAllQuery, IReadOnlyList<ProcessTemplateDto>>
{
    private readonly IProcessTemplateRepository _repository;

    public ProcessTemplateGetAllQueryHandler(IProcessTemplateRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<ProcessTemplateDto>> Handle(
        ProcessTemplateGetAllQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return items.Select(ProcessTemplateMapper.ToDto).ToList();
    }
}

public class ProcessTemplateGetByIdQueryHandler
    : IRequestHandler<ProcessTemplateGetByIdQuery, ProcessTemplateDto?>
{
    private readonly IProcessTemplateRepository _repository;

    public ProcessTemplateGetByIdQueryHandler(IProcessTemplateRepository repository) => _repository = repository;

    public async Task<ProcessTemplateDto?> Handle(
        ProcessTemplateGetByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdWithStepsAsync(request.Id, cancellationToken);
        return entity is null ? null : ProcessTemplateMapper.ToDto(entity);
    }
}

public class ProcessTemplateGetPagedQueryHandler
    : IRequestHandler<ProcessTemplateGetPagedQuery, PagedResult<ProcessTemplateDto>>
{
    private readonly IProcessTemplateRepository _repository;

    public ProcessTemplateGetPagedQueryHandler(IProcessTemplateRepository repository) => _repository = repository;

    public async Task<PagedResult<ProcessTemplateDto>> Handle(
        ProcessTemplateGetPagedQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.Keyword, request.Page, request.PageSize, cancellationToken);

        return new PagedResult<ProcessTemplateDto>(
            items.Select(ProcessTemplateMapper.ToDto).ToList(), total, request.Page, request.PageSize);
    }
}
