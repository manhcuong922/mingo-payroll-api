using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Payroll;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Common;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Queries;

public sealed record GetPaymentObligationByIdQuery(int Id) : IRequest<PaymentObligationDto?>;

public sealed class GetPaymentObligationByIdQueryHandler
    : IRequestHandler<GetPaymentObligationByIdQuery, PaymentObligationDto?>
{
    private readonly IPaymentObligationRepository _repo;
    private readonly IClock _clock;

    public GetPaymentObligationByIdQueryHandler(IPaymentObligationRepository repo, IClock clock)
    {
        _repo = repo;
        _clock = clock;
    }

    public async Task<PaymentObligationDto?> Handle(GetPaymentObligationByIdQuery request, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(request.Id, asNoTracking: true, ct);
        if (item is null) return null;

        var counts = await _repo.CountLinesByStatusAsync(item.Id, ct);
        var aggregates = await _repo.GetLineAggregatesAsync(item.Id, ct);
        var evidenceCount = await _repo.CountEvidenceAsync(item.Id, ct);

        return PaymentObligationMapper.Map(item, _clock.UtcNow.Date, counts, aggregates, evidenceCount);
    }
}
