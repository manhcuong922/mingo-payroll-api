using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Payroll;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Common;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Queries;

public sealed record GetPaymentObligationsQuery(
    int? Year,
    int? Month,
    PaymentObligationType? Type,
    PaymentObligationStatus? Status) : IRequest<IReadOnlyList<PaymentObligationDto>>;

public sealed class GetPaymentObligationsQueryHandler
    : IRequestHandler<GetPaymentObligationsQuery, IReadOnlyList<PaymentObligationDto>>
{
    private readonly IPaymentObligationRepository _repo;
    private readonly IClock _clock;

    public GetPaymentObligationsQueryHandler(IPaymentObligationRepository repo, IClock clock)
    {
        _repo = repo;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PaymentObligationDto>> Handle(
        GetPaymentObligationsQuery request, CancellationToken ct)
    {
        var items = await _repo.QueryAsync(request.Year, request.Month, request.Type, request.Status, ct);
        var today = _clock.UtcNow.Date;

        var result = new List<PaymentObligationDto>(items.Count);
        foreach (var o in items)
        {
            var counts = await _repo.CountLinesByStatusAsync(o.Id, ct);
            var aggregates = await _repo.GetLineAggregatesAsync(o.Id, ct);
            var evidenceCount = await _repo.CountEvidenceAsync(o.Id, ct);
            result.Add(PaymentObligationMapper.Map(o, today, counts, aggregates, evidenceCount));
        }
        return result;
    }
}
