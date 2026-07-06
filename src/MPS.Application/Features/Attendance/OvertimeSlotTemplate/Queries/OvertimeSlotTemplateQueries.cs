using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.OvertimeSlotTemplate.Common;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeSlotTemplate.Queries;

public sealed record GetOvertimeSlotTemplatesQuery(
    bool? IsActive,
    int Page = 1,
    int Size = 50) : IRequest<OvertimeSlotTemplatePagedResult>;

public sealed record GetOvertimeSlotTemplateByIdQuery(int Id) : IRequest<OvertimeSlotTemplateDto?>;

public sealed record GetActiveOvertimeSlotTemplatesQuery()
    : IRequest<IReadOnlyList<OvertimeSlotTemplateDto>>;

public sealed record OvertimeSlotTemplatePagedResult(
    IReadOnlyList<OvertimeSlotTemplateDto> Items,
    int Total,
    int Page,
    int Size);

public sealed class GetOvertimeSlotTemplatesQueryHandler
    : IRequestHandler<GetOvertimeSlotTemplatesQuery, OvertimeSlotTemplatePagedResult>
{
    private readonly IOvertimeSlotTemplateRepository _repository;

    public GetOvertimeSlotTemplatesQueryHandler(IOvertimeSlotTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<OvertimeSlotTemplatePagedResult> Handle(
        GetOvertimeSlotTemplatesQuery request, CancellationToken ct)
    {
        var (items, total) = await _repository.GetPagedAsync(request.IsActive, request.Page, request.Size, ct);
        var dtos = items.Select(OvertimeSlotTemplateMapper.ToDto).ToList();
        return new OvertimeSlotTemplatePagedResult(dtos, total, request.Page, request.Size);
    }
}

public sealed class GetOvertimeSlotTemplateByIdQueryHandler
    : IRequestHandler<GetOvertimeSlotTemplateByIdQuery, OvertimeSlotTemplateDto?>
{
    private readonly IOvertimeSlotTemplateRepository _repository;

    public GetOvertimeSlotTemplateByIdQueryHandler(IOvertimeSlotTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<OvertimeSlotTemplateDto?> Handle(GetOvertimeSlotTemplateByIdQuery request, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(request.Id, ct);
        return item is null ? null : OvertimeSlotTemplateMapper.ToDto(item);
    }
}

public sealed class GetActiveOvertimeSlotTemplatesQueryHandler
    : IRequestHandler<GetActiveOvertimeSlotTemplatesQuery, IReadOnlyList<OvertimeSlotTemplateDto>>
{
    private readonly IOvertimeSlotTemplateRepository _repository;

    public GetActiveOvertimeSlotTemplatesQueryHandler(IOvertimeSlotTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<OvertimeSlotTemplateDto>> Handle(
        GetActiveOvertimeSlotTemplatesQuery request, CancellationToken ct)
    {
        var items = await _repository.GetActiveAsync(ct);
        return items.Select(OvertimeSlotTemplateMapper.ToDto).ToList();
    }
}
