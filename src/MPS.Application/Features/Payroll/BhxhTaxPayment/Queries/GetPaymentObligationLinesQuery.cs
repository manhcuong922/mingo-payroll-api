using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Payroll;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Common;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Queries;

/// <summary>
/// Lấy danh sách line (NV) của 1 obligation, có filter status + search theo mã/tên snapshot,
/// paginated. PageSize clamp [1, 200] để chặn dump toàn bảng.
/// </summary>
public sealed record GetPaymentObligationLinesQuery(
    int ObligationId,
    PaymentLineStatus? StatusFilter,
    string? Search,
    int Page = 1,
    int PageSize = 50) : IRequest<PagedResult<PaymentObligationLineDto>>;

public sealed class GetPaymentObligationLinesQueryHandler
    : IRequestHandler<GetPaymentObligationLinesQuery, PagedResult<PaymentObligationLineDto>>
{
    private const int MaxPageSize = 200;

    private readonly IPaymentObligationRepository _repo;

    public GetPaymentObligationLinesQueryHandler(IPaymentObligationRepository repo) => _repo = repo;

    public async Task<PagedResult<PaymentObligationLineDto>> Handle(
        GetPaymentObligationLinesQuery request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 1 : Math.Min(request.PageSize, MaxPageSize);

        var (items, total) = await _repo.GetLinesByObligationAsync(
            request.ObligationId, request.StatusFilter, request.Search, page, pageSize, ct);

        var dtos = items.Select(PaymentObligationMapper.MapLine).ToList();
        return new PagedResult<PaymentObligationLineDto>(dtos, total, page, pageSize);
    }
}
