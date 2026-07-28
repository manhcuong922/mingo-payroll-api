using System.Globalization;
using System.Text;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.Queries;

/// <summary>
/// NV tải phiếu lương .xlsx của CHÍNH MÌNH (kỳ Finalized/Locked). Dùng lại builder + exporter của
/// bảng lương HR trên payload đã filter còn 1 row → sheet "4. LUONG" chỉ có dòng của NV; kèm sheet
/// tham số (biểu thuế, tỷ lệ BHXH) + sheet ghi chú làm căn cứ minh bạch số tiền.
/// Đặt tên file theo NV: <c>phieu-luong-{yyyy}-{MM}-{slug-ho-ten}.xlsx</c>.
/// </summary>
public sealed record ExportMyPayslipQuery(int PeriodId) : IRequest<PayrollExportFileDto>;

public sealed class ExportMyPayslipQueryHandler
    : IRequestHandler<ExportMyPayslipQuery, PayrollExportFileDto>
{
    private readonly IPayrollPeriodRepository _periods;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IPayrollExcelExporter _exporter;
    private readonly IPayrollFieldCatalog _catalog;

    public ExportMyPayslipQueryHandler(
        IPayrollPeriodRepository periods,
        IContractLookupReader lookup,
        ICurrentUserService currentUser,
        IPayrollExcelExporter exporter,
        IPayrollFieldCatalog catalog)
    {
        _periods = periods;
        _lookup = lookup;
        _currentUser = currentUser;
        _exporter = exporter;
        _catalog = catalog;
    }

    public async Task<PayrollExportFileDto> Handle(ExportMyPayslipQuery request, CancellationToken ct)
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

        var book = new PayrollExcelBookBuilder(_catalog).Build(period);
        var bytes = _exporter.Build(book);
        var fileName = BuildFileName(period.PeriodYear, period.PeriodMonth,
            period.Rows.Single().EmployeeInformation?.FullName);

        return new PayrollExportFileDto(bytes, fileName, ExportPayrollPeriodQueryHandler.XlsxContentType);
    }

    private static string BuildFileName(int year, int month, string? fullName)
    {
        var slug = Slugify(fullName);
        return string.IsNullOrEmpty(slug)
            ? $"phieu-luong-{year:0000}-{month:00}.xlsx"
            : $"phieu-luong-{year:0000}-{month:00}-{slug}.xlsx";
    }

    /// <summary>Bỏ dấu, thay ký tự ngoài [a-z0-9] bằng dấu gạch. "Nguyễn Thị Mai" → "nguyen-thi-mai".</summary>
    private static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (ch == 'đ' || ch == 'Đ') { sb.Append('d'); continue; }
            sb.Append(char.ToLowerInvariant(ch));
        }

        var ascii = sb.ToString();
        var slug = new StringBuilder(ascii.Length);
        var lastDash = true;
        foreach (var ch in ascii)
        {
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                slug.Append(ch);
                lastDash = false;
            }
            else if (!lastDash)
            {
                slug.Append('-');
                lastDash = true;
            }
        }
        return slug.ToString().Trim('-');
    }
}
