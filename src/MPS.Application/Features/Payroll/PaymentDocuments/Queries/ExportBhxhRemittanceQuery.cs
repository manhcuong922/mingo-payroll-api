using System.Globalization;
using MediatR;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Text;
using MPS.Application.Features.Payroll.PaymentDocuments.Common;
using MPS.Application.Features.Payroll.Queries;
using MPS.Domain.Exceptions;
using Cells = MPS.Application.Features.Payroll.PaymentDocuments.Common.BhxhRemittanceFormCells;

namespace MPS.Application.Features.Payroll.PaymentDocuments.Queries;

/// <summary>
/// Sinh ủy nhiệm chi nộp BHXH/BHYT/BHTN của kỳ (Agribank, mẫu DP004).
///
/// <para>Sheet in chạy hoàn toàn bằng công thức trỏ về sheet dữ liệu <c>Nguon</c>, nên chỉ ghi
/// 10 ô vô hướng — bố cục bản in không bị đụng tới.</para>
///
/// <para>Nội dung chuyển khoản theo Công văn 1995/BHXH-TCKT do <see cref="BhxhRemittanceContentBuilder"/>
/// dựng và sẽ NÉM nếu mã đơn vị/mã cơ quan sai. Cố tình không bắt lỗi ở đây: sai cú pháp thì tiền
/// vẫn chuyển đi nhưng cơ quan BHXH không đối soát tự động được, quá trình đóng của NLĐ bị treo.</para>
/// </summary>
public sealed record ExportBhxhRemittanceQuery(int PeriodId) : IRequest<PayrollExportFileDto>;

public sealed class ExportBhxhRemittanceQueryHandler
    : IRequestHandler<ExportBhxhRemittanceQuery, PayrollExportFileDto>
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private readonly PaymentDocumentContextLoader _loader;
    private readonly IPaymentTemplateProvider _templates;
    private readonly IXlsxTemplateFiller _filler;

    public ExportBhxhRemittanceQueryHandler(
        PaymentDocumentContextLoader loader,
        IPaymentTemplateProvider templates,
        IXlsxTemplateFiller filler)
    {
        _loader = loader;
        _templates = templates;
        _filler = filler;
    }

    public async Task<PayrollExportFileDto> Handle(ExportBhxhRemittanceQuery request, CancellationToken ct)
    {
        var context = await _loader.LoadAsync(request.PeriodId, ct);

        var missing = PaymentSettingRequirements.ForBhxhRemittance(context.Setting);
        if (missing.Count > 0)
        {
            throw new DomainValidationException(
                "Chưa lập được ủy nhiệm chi nộp BHXH, còn thiếu trong cấu hình chi trả: "
                + string.Join(", ", missing) + ".");
        }

        var setting = context.Setting!;
        var period = context.Period;
        var (employeePart, employerPart) = PaymentDocumentContextLoader.SumBhxhContributions(period);
        var total = employeePart + employerPart;

        if (total <= 0)
        {
            throw new DomainValidationException(
                "Kỳ lương này không phát sinh khoản phải nộp BHXH nào — không lập ủy nhiệm chi.");
        }

        var payerName = setting.SalaryAccountName ?? context.Company?.LegalName
            ?? throw new DomainValidationException(
                "Chưa khai bên ký hợp đồng (pháp nhân) — ủy nhiệm chi không có tên bên trả tiền.");

        var issueDate = period.PayDate.ToDateTime(TimeOnly.MinValue);

        var edits = new List<XlsxCellEdit>
        {
            XlsxCellEdit.OfText(Cells.DataSheet, Cells.DocumentNo,
                $"BHXH-{period.PeriodMonth:00}/{period.PeriodYear}"),
            XlsxCellEdit.OfText(Cells.DataSheet, Cells.DocumentDate,
                issueDate.ToString("dd/MM/yyyy", Culture)),

            XlsxCellEdit.OfText(Cells.DataSheet, Cells.Description,
                BhxhRemittanceContentBuilder.Build(setting.BhxhUnitCode, setting.BhxhAgencyCode)),
            XlsxCellEdit.OfNumber(Cells.DataSheet, Cells.AmountFigures, total),
            XlsxCellEdit.OfText(Cells.DataSheet, Cells.AmountWords, VietnameseMoneyWords.ToWords(total)),

            XlsxCellEdit.OfText(Cells.DataSheet, Cells.PayerName, payerName),
            XlsxCellEdit.OfText(Cells.DataSheet, Cells.PayerAccountNo, setting.BhxhPayerAccountNo),
            XlsxCellEdit.OfText(Cells.DataSheet, Cells.PayerBankName, setting.BhxhPayerBankBranch),

            XlsxCellEdit.OfText(Cells.DataSheet, Cells.BeneficiaryName, setting.BhxhAgencyName),
            XlsxCellEdit.OfText(Cells.DataSheet, Cells.BeneficiaryAccountNo, setting.BhxhBeneficiaryAccountNo),
        };

        using var template = _templates.Open(PaymentTemplateKind.BhxhRemittanceOrder);
        var content = _filler.Fill(template, edits);

        return new PayrollExportFileDto(
            content,
            $"uy-nhiem-chi-bhxh-{period.PeriodMonth:00}-{period.PeriodYear}.xlsx",
            PaymentDocumentContentTypes.Xlsx);
    }
}
