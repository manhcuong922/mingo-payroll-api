using MediatR;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.Queries;

public sealed record PayrollExportFileDto(byte[] Content, string FileName, string ContentType);

/// <summary>Xuất bảng lương ra Excel — công thức thật, ghi chú trần/đổi luật, tham số của kỳ.</summary>
public sealed record ExportPayrollPeriodQuery(int PeriodId) : IRequest<PayrollExportFileDto>;

public sealed class ExportPayrollPeriodQueryHandler
    : IRequestHandler<ExportPayrollPeriodQuery, PayrollExportFileDto>
{
    public const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IPayrollPeriodRepository _periods;
    private readonly IPayrollExcelExporter _exporter;
    private readonly IPayrollFieldCatalog _catalog;

    public ExportPayrollPeriodQueryHandler(
        IPayrollPeriodRepository periods, IPayrollExcelExporter exporter, IPayrollFieldCatalog catalog)
    {
        _periods = periods;
        _exporter = exporter;
        _catalog = catalog;
    }

    public async Task<PayrollExportFileDto> Handle(ExportPayrollPeriodQuery request, CancellationToken ct)
    {
        var period = await _periods.GetFullAsync(request.PeriodId, asNoTracking: true, ct)
            ?? throw new NotFoundException($"Không tìm thấy kỳ lương #{request.PeriodId}.");

        var book = new PayrollExcelBookBuilder(_catalog).Build(period);

        return new PayrollExportFileDto(_exporter.Build(book), book.FileName, XlsxContentType);
    }
}
