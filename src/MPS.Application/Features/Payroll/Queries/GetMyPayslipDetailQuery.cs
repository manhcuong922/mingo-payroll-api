using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Payroll;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.Queries;

/// <summary>
/// NV xem chi tiết phiếu lương của CHÍNH MÌNH trong 1 kỳ (Finalized/Locked). Trả về payload khớp
/// 1-1 với <see cref="PayrollSheetDto"/> (FE tái dùng bảng lương full) — chỉ khác là <c>Rows</c>
/// đúng 1 phần tử (dòng của NV), không có dòng Total.
/// Kỳ Draft, kỳ không tồn tại, hoặc NV không có dòng trong kỳ đều trả 404 (không lộ thông tin kỳ).
/// </summary>
public sealed record GetMyPayslipDetailQuery(int PeriodId) : IRequest<MyPayslipDetailDto>;

public sealed class GetMyPayslipDetailQueryHandler
    : IRequestHandler<GetMyPayslipDetailQuery, MyPayslipDetailDto>
{
    private readonly IPayrollPeriodRepository _periods;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;

    public GetMyPayslipDetailQueryHandler(
        IPayrollPeriodRepository periods,
        IContractLookupReader lookup,
        ICurrentUserService currentUser)
    {
        _periods = periods;
        _lookup = lookup;
        _currentUser = currentUser;
    }

    public async Task<MyPayslipDetailDto> Handle(GetMyPayslipDetailQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, ct)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        var period = await _periods.GetPeriodWithEmployeeRowAsync(request.PeriodId, employeeId, ct);
        if (period is null
            || period.Status == PayrollPeriodStatus.Draft
            || period.Rows.Count == 0)
        {
            throw new NotFoundException($"Không tìm thấy phiếu lương kỳ #{request.PeriodId}.");
        }

        var sheet = PayrollSheetMapper.Map(period);
        var row = period.Rows.Single();
        var employee = row.EmployeeInformation
            ?? throw new NotFoundException($"Không tìm thấy phiếu lương kỳ #{request.PeriodId}.");

        var header = new EmployeePayslipHeaderDto(
            employee.Id,
            employee.FullName,
            employee.Position?.Name,
            LastFour(employee.BankNumber));

        return new MyPayslipDetailDto(
            sheet,
            header,
            EffectiveValue(row, PayrollColumnCodes.TongThuNhap),
            EffectiveValue(row, PayrollColumnCodes.ThucNhan));
    }

    private static decimal? EffectiveValue(PayrollRow row, string columnCode)
    {
        var cell = row.Cells.FirstOrDefault(c =>
            string.Equals(c.ColumnCode, columnCode, StringComparison.OrdinalIgnoreCase));
        if (cell is null) return null;
        return cell.IsOverridden ? cell.OverrideValue : cell.ComputedValue;
    }

    private static string? LastFour(string? bankNumber)
    {
        if (string.IsNullOrWhiteSpace(bankNumber)) return null;
        var digits = new string(bankNumber.Where(char.IsDigit).ToArray());
        return digits.Length <= 4 ? digits : digits[^4..];
    }
}
