using MediatR;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Features.Payroll.PaymentDocuments.Common;
using MPS.Application.Features.Payroll.Queries;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.PaymentDocuments.Queries;

/// <summary>
/// Sinh "Đề nghị thanh toán lương kiêm ủy nhiệm chi" (VPBank, mẫu MB01.QT-NVTT/07) từ kỳ lương.
///
/// <para>Điền lên chính biểu mẫu ngân hàng phát hành: giữ nguyên bố cục, khung, chữ ký, chỉ đổ số
/// vào đúng ô. Danh sách dài hơn số dòng kẻ sẵn thì chèn thêm dòng và dịch toàn bộ tham chiếu.</para>
///
/// <para>CHẶN khi cấu hình chi trả còn thiếu, thay vì xuất chứng từ khuyết ô — lệnh chi thiếu số
/// tài khoản trích nợ là ngân hàng trả lại, còn kế toán thì đã tưởng xong việc.</para>
/// </summary>
public sealed record ExportSalaryPaymentOrderQuery(int PeriodId) : IRequest<PayrollExportFileDto>;

public sealed class ExportSalaryPaymentOrderQueryHandler
    : IRequestHandler<ExportSalaryPaymentOrderQuery, PayrollExportFileDto>
{
    private readonly PaymentDocumentContextLoader _loader;
    private readonly ICompanyPaymentSettingRepository _settings;
    private readonly IPaymentTemplateProvider _templates;
    private readonly IXlsxTemplateFiller _filler;

    public ExportSalaryPaymentOrderQueryHandler(
        PaymentDocumentContextLoader loader,
        ICompanyPaymentSettingRepository settings,
        IPaymentTemplateProvider templates,
        IXlsxTemplateFiller filler)
    {
        _loader = loader;
        _settings = settings;
        _templates = templates;
        _filler = filler;
    }

    public async Task<PayrollExportFileDto> Handle(ExportSalaryPaymentOrderQuery request, CancellationToken ct)
    {
        var context = await _loader.LoadAsync(request.PeriodId, ct);

        var missing = PaymentSettingRequirements.ForSalaryOrder(context.Setting);
        if (missing.Count > 0)
        {
            throw new DomainValidationException(
                "Chưa lập được lệnh chi lương, còn thiếu trong cấu hình chi trả: "
                + string.Join(", ", missing) + ".");
        }

        var company = context.Company
            ?? throw new DomainValidationException(
                "Chưa khai bên ký hợp đồng (pháp nhân) — lệnh chi lương không có tên và địa chỉ công ty.");

        var provinces = (await _settings.GetProvincesAsync(ct))
            .ToDictionary(p => p.Code, p => p.FormName, StringComparer.OrdinalIgnoreCase);

        var setting = context.Setting!;
        var plan = SalaryPaymentPlanBuilder.Build(
            context.Period.Rows, setting.SalaryBankCode!.Trim(), provinces);

        if (plan.TotalCount == 0)
        {
            throw new DomainValidationException(
                "Không có nhân viên nào đủ điều kiện chuyển lương trong kỳ này — "
                + "kiểm tra lại số tài khoản và ngân hàng trên hồ sơ nhân sự.");
        }

        var period = context.Period;
        var valueDate = period.PayDate.ToDateTime(TimeOnly.MinValue);

        var insertions = SalaryPaymentEditBuilder.BuildInsertions(plan);
        var edits = SalaryPaymentEditBuilder.BuildEdits(
            plan, setting, company, period.PeriodYear, period.PeriodMonth, valueDate);

        using var template = _templates.Open(PaymentTemplateKind.SalaryPaymentOrder);
        var content = _filler.Fill(template, insertions, edits);

        return new PayrollExportFileDto(
            content,
            $"lenh-chi-luong-{period.PeriodMonth:00}-{period.PeriodYear}.xlsx",
            PaymentDocumentContentTypes.Xlsx);
    }
}
