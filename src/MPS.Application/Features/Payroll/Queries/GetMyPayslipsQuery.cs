using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Payroll;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.Queries;

/// <summary>
/// NV xem phiếu lương của CHÍNH MÌNH trong các kỳ đã chốt (Finalized/Locked), mới nhất trước.
/// Kỳ Draft không trả về — số liệu chưa chốt, chưa phải phiếu lương.
/// </summary>
public sealed record GetMyPayslipsQuery(int Months) : IRequest<IReadOnlyList<MyPayslipDto>>;

public sealed class GetMyPayslipsQueryHandler
    : IRequestHandler<GetMyPayslipsQuery, IReadOnlyList<MyPayslipDto>>
{
    private const int DefaultMonths = 6;
    private const int MaxMonths = 24;

    private readonly IPayrollPeriodRepository _periods;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;

    public GetMyPayslipsQueryHandler(
        IPayrollPeriodRepository periods,
        IContractLookupReader lookup,
        ICurrentUserService currentUser)
    {
        _periods = periods;
        _lookup = lookup;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MyPayslipDto>> Handle(GetMyPayslipsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, ct)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        var months = request.Months <= 0 ? DefaultMonths : Math.Min(request.Months, MaxMonths);
        var rows = await _periods.GetEmployeeRowsAsync(employeeId, months, ct);

        return rows
            .Select(r => new MyPayslipDto(
                r.PayrollPeriod.Id,
                r.PayrollPeriod.PeriodYear,
                r.PayrollPeriod.PeriodMonth,
                r.PayrollPeriod.PayDate,
                r.PayrollPeriod.Status.ToString(),
                EffectiveValue(r, PayrollColumnCodes.TongThuNhap),
                EffectiveValue(r, PayrollColumnCodes.ThucNhan)))
            .ToList();
    }

    private static decimal? EffectiveValue(PayrollRow row, string columnCode)
    {
        var cell = row.Cells.FirstOrDefault(c =>
            string.Equals(c.ColumnCode, columnCode, StringComparison.OrdinalIgnoreCase));
        if (cell is null) return null;
        return cell.IsOverridden ? cell.OverrideValue : cell.ComputedValue;
    }
}
