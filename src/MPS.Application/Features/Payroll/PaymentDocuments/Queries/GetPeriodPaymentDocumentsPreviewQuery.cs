using MediatR;
using MPS.Application.Common.Payroll;
using MPS.Application.Features.Payroll.PaymentDocuments.Common;
using MPS.Domain.Entities.Payroll.Banking;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.PaymentDocuments.Queries;

/// <summary>
/// Xem trước 2 chứng từ lập theo kỳ trước khi tải file.
///
/// <para>Mục đích chính là CHẶN TRƯỚC: lệnh chi lương thiếu chi nhánh của một NV thì VPBank trả
/// lại nguyên lệnh, còn ủy nhiệm chi sai cú pháp nội dung thì tiền vẫn đi nhưng cơ quan BHXH
/// không đối soát được. Cả hai lỗi ấy hiện ra ở đây, trước khi kế toán mang chứng từ ra ngân hàng.</para>
/// </summary>
public sealed record GetPeriodPaymentDocumentsPreviewQuery(int PeriodId)
    : IRequest<PeriodPaymentDocumentsPreviewDto>;

public sealed class GetPeriodPaymentDocumentsPreviewQueryHandler
    : IRequestHandler<GetPeriodPaymentDocumentsPreviewQuery, PeriodPaymentDocumentsPreviewDto>
{
    private readonly PaymentDocumentContextLoader _loader;
    private readonly ICompanyPaymentSettingRepository _settings;

    public GetPeriodPaymentDocumentsPreviewQueryHandler(
        PaymentDocumentContextLoader loader,
        ICompanyPaymentSettingRepository settings)
    {
        _loader = loader;
        _settings = settings;
    }

    public async Task<PeriodPaymentDocumentsPreviewDto> Handle(
        GetPeriodPaymentDocumentsPreviewQuery request, CancellationToken ct)
    {
        var context = await _loader.LoadAsync(request.PeriodId, ct);

        return new PeriodPaymentDocumentsPreviewDto(
            PeriodId: context.Period.Id,
            PeriodYear: context.Period.PeriodYear,
            PeriodMonth: context.Period.PeriodMonth,
            PeriodStatus: context.Period.Status.ToString(),
            SalaryOrder: await BuildSalaryPreviewAsync(context, ct),
            BhxhRemittance: BuildBhxhPreview(context));
    }

    private async Task<SalaryPaymentPreviewDto> BuildSalaryPreviewAsync(
        PaymentDocumentContext context, CancellationToken ct)
    {
        var missing = new List<string>(PaymentSettingRequirements.ForSalaryOrder(context.Setting));
        if (context.Company is null) missing.Add("Bên ký hợp đồng (tên + địa chỉ pháp nhân)");

        var inHouseBankCode = context.Setting?.SalaryBankCode?.Trim();
        BankCatalog? bank = null;

        // Chưa khai ngân hàng chủ quản thì không chia được 2 bảng — vẫn dựng kế hoạch với mã rỗng
        // để kế toán thấy được danh sách NV bị loại vì thiếu hồ sơ, thay vì thấy màn hình trắng.
        if (!string.IsNullOrWhiteSpace(inHouseBankCode))
        {
            bank = await _settings.FindBankAsync(inHouseBankCode, ct);
        }

        var provinces = await LoadProvinceFormNamesAsync(ct);
        var plan = SalaryPaymentPlanBuilder.Build(
            context.Period.Rows, inHouseBankCode ?? string.Empty, provinces);

        return new SalaryPaymentPreviewDto(
            MissingSettings: missing,
            SalaryBankCode: inHouseBankCode,
            SalaryBankName: bank?.FullName,
            InHouseCount: plan.InHouseBank.Count,
            InHouseAmount: plan.InHouseBank.Sum(b => b.Amount),
            OtherBanksCount: plan.OtherBanks.Count,
            OtherBanksAmount: plan.OtherBanks.Sum(b => b.Amount),
            TotalCount: plan.TotalCount,
            TotalAmount: plan.TotalAmount,
            Excluded: plan.Excluded
                .Select(e => new SalaryPaymentExclusionDto(e.EmployeeCode, e.FullName, e.Amount, e.Reason))
                .ToList());
    }

    private static BhxhRemittancePreviewDto BuildBhxhPreview(PaymentDocumentContext context)
    {
        var missing = PaymentSettingRequirements.ForBhxhRemittance(context.Setting);
        var (employee, employer) = PaymentDocumentContextLoader.SumBhxhContributions(context.Period);

        string? content = null;
        string? contentError = null;

        try
        {
            content = BhxhRemittanceContentBuilder.Build(
                context.Setting?.BhxhUnitCode, context.Setting?.BhxhAgencyCode);
        }
        catch (ArgumentException ex)
        {
            // Builder ném khi mã sai cú pháp. Ở màn hình xem trước thì đó là thông tin cần hiển thị,
            // không phải lỗi hệ thống — lệnh xuất mới là chỗ chặn.
            contentError = ex.Message;
        }

        return new BhxhRemittancePreviewDto(
            MissingSettings: missing,
            EmployeeContribution: employee,
            EmployerContribution: employer,
            TotalAmount: employee + employer,
            RemittanceContent: content,
            ContentError: contentError);
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadProvinceFormNamesAsync(CancellationToken ct)
    {
        var provinces = await _settings.GetProvincesAsync(ct);
        return provinces.ToDictionary(p => p.Code, p => p.FormName, StringComparer.OrdinalIgnoreCase);
    }
}
