using MediatR;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.DTOs.Payroll;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.Queries;

// ===== Đủ liệu (preview trước khi tạo) =====
public sealed record GetPayrollReadinessQuery(int EmployerPartyId, int Year, int Month)
    : IRequest<IReadOnlyList<PayrollReadinessDto>>;

public sealed class GetPayrollReadinessQueryHandler
    : IRequestHandler<GetPayrollReadinessQuery, IReadOnlyList<PayrollReadinessDto>>
{
    private readonly IPayrollReadinessChecker _readiness;
    public GetPayrollReadinessQueryHandler(IPayrollReadinessChecker readiness) => _readiness = readiness;

    public async Task<IReadOnlyList<PayrollReadinessDto>> Handle(GetPayrollReadinessQuery request, CancellationToken ct)
    {
        var result = await _readiness.CheckAsync(
            new PayrollReadinessRequest(request.EmployerPartyId, request.Year, request.Month), ct);
        return result.Select(r => new PayrollReadinessDto(
            r.EmployeeInformationId, r.EmployeeCode, r.FullName, r.IsReady, r.Missing)).ToList();
    }
}

// ===== Danh sách kỳ lương =====
public sealed record GetPayrollPeriodsQuery(int? Year, int? Month)
    : IRequest<IReadOnlyList<PayrollPeriodSummaryDto>>;

public sealed class GetPayrollPeriodsQueryHandler
    : IRequestHandler<GetPayrollPeriodsQuery, IReadOnlyList<PayrollPeriodSummaryDto>>
{
    private readonly IPayrollPeriodRepository _periods;
    public GetPayrollPeriodsQueryHandler(IPayrollPeriodRepository periods) => _periods = periods;

    public async Task<IReadOnlyList<PayrollPeriodSummaryDto>> Handle(GetPayrollPeriodsQuery request, CancellationToken ct)
    {
        var periods = await _periods.GetAllAsync(request.Year, request.Month, ct);
        return periods.Select(p => new PayrollPeriodSummaryDto(
            p.Id, p.PeriodYear, p.PeriodMonth, p.Status.ToString(), p.PayDate, 0)).ToList();
    }
}
