using System.IO.Compression;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Features.Payroll.PaymentDocuments.Common;
using MPS.Application.Features.Payroll.Queries;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.PaymentDocuments.Queries;

/// <summary>
/// Sinh tờ khai quyết toán thuế TNCN mẫu 02/QTT-TNCN cho một năm.
///
/// <para>Một NV ⇒ một file .docx; nhiều NV ⇒ một file .zip gồm từng tờ khai riêng, vì đây là tờ
/// khai của CÁ NHÂN người lao động (mỗi người ký và nộp bản của mình), không phải một biểu tổng
/// hợp của doanh nghiệp.</para>
/// </summary>
/// <param name="EmployeeInformationId">null = xuất cho mọi NV có kỳ lương đã chốt trong năm.</param>
public sealed record ExportPitFinalizationQuery(int Year, int? EmployeeInformationId)
    : IRequest<PayrollExportFileDto>;

public sealed class ExportPitFinalizationQueryHandler
    : IRequestHandler<ExportPitFinalizationQuery, PayrollExportFileDto>
{
    private readonly IPayrollPeriodRepository _periods;
    private readonly PaymentDocumentContextLoader _loader;
    private readonly IPaymentTemplateProvider _templates;
    private readonly IDocumentRenderingService _renderer;

    public ExportPitFinalizationQueryHandler(
        IPayrollPeriodRepository periods,
        PaymentDocumentContextLoader loader,
        IPaymentTemplateProvider templates,
        IDocumentRenderingService renderer)
    {
        _periods = periods;
        _loader = loader;
        _templates = templates;
        _renderer = renderer;
    }

    public async Task<PayrollExportFileDto> Handle(ExportPitFinalizationQuery request, CancellationToken ct)
    {
        var employer = await _loader.LoadEmployerAsync(ct)
            ?? throw new DomainValidationException(
                "Chưa khai bên ký hợp đồng (pháp nhân) — tờ khai quyết toán thuế thiếu tên và mã số thuế "
                + "của tổ chức trả thu nhập.");

        var rows = await _periods.GetRowsByYearAsync(request.Year, request.EmployeeInformationId, ct);
        if (rows.Count == 0)
        {
            throw new DomainValidationException(
                $"Năm {request.Year} chưa có kỳ lương nào được chốt — không có số liệu để quyết toán thuế.");
        }

        var brackets = PitFinalizationBrackets.Resolve(rows);
        var employees = rows
            .GroupBy(r => r.EmployeeInformationId!.Value)
            .Select(group => (IReadOnlyList<PayrollRow>)group.ToList())
            .OrderBy(group => group[0].EmployeeInformation!.User?.EmployeeCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (employees.Count == 1)
        {
            var single = await RenderAsync(employees[0], employer, request.Year, brackets, ct);
            return new PayrollExportFileDto(single.Content, single.FileName, PaymentDocumentContentTypes.Docx);
        }

        return new PayrollExportFileDto(
            await RenderArchiveAsync(employees, employer, request.Year, brackets, ct),
            $"02-QTT-TNCN-{request.Year}.zip",
            PaymentDocumentContentTypes.Zip);
    }

    private async Task<byte[]> RenderArchiveAsync(
        IReadOnlyList<IReadOnlyList<PayrollRow>> employees,
        PitFinalizationEmployer employer,
        int year,
        IReadOnlyList<PitBracketSnapshot> brackets,
        CancellationToken ct)
    {
        using var output = new MemoryStream();

        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var employeeRows in employees)
            {
                var (fileName, content) = await RenderAsync(employeeRows, employer, year, brackets, ct);

                var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                await entryStream.WriteAsync(content, ct);
            }
        }

        return output.ToArray();
    }

    private async Task<(string FileName, byte[] Content)> RenderAsync(
        IReadOnlyList<PayrollRow> employeeRows,
        PitFinalizationEmployer employer,
        int year,
        IReadOnlyList<PitBracketSnapshot> brackets,
        CancellationToken ct)
    {
        var employee = employeeRows[0].EmployeeInformation!;
        var values = PitFinalizationDocumentBuilder.Build(employee, employer, year, employeeRows, brackets);

        // Mỗi tờ khai mở một luồng biểu mẫu MỚI: bộ render đọc hết stream, tái dùng sẽ ra file hỏng.
        using var template = _templates.Open(PaymentTemplateKind.PitFinalization);
        using var rendered = await _renderer.RenderAsync(template, values, ct);
        using var buffer = new MemoryStream();

        rendered.Position = 0;
        await rendered.CopyToAsync(buffer, ct);

        return (
            PitFinalizationDocumentBuilder.BuildFileName(employee, employee.User?.EmployeeCode, year),
            buffer.ToArray());
    }
}
