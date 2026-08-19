using MediatR;
using MPS.Application.Common.Payroll;
using MPS.Application.Features.Payroll.PaymentDocuments.Common;
using MPS.Domain.Entities.Payroll.Banking;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.CompanyPaymentSettings.Queries;

/// <summary>
/// Cấu hình chi trả hiện tại. Trả DTO rỗng (mọi field null) khi công ty chưa khai lần nào —
/// màn hình cấu hình là form khai lần đầu, không phải trang 404.
/// </summary>
public sealed record GetCompanyPaymentSettingQuery : IRequest<CompanyPaymentSettingDto>;

public sealed class GetCompanyPaymentSettingQueryHandler
    : IRequestHandler<GetCompanyPaymentSettingQuery, CompanyPaymentSettingDto>
{
    private readonly ICompanyPaymentSettingRepository _repository;

    public GetCompanyPaymentSettingQueryHandler(ICompanyPaymentSettingRepository repository)
        => _repository = repository;

    public async Task<CompanyPaymentSettingDto> Handle(
        GetCompanyPaymentSettingQuery request, CancellationToken ct)
    {
        var setting = await _repository.GetAsync(ct);

        var salaryBank = await _repository.FindBankAsync(setting?.SalaryBankCode, ct);
        var payerBank = await _repository.FindBankAsync(setting?.BhxhPayerBankCode, ct);

        var (content, contentError) = BuildRemittanceContent(setting);

        return new CompanyPaymentSettingDto(
            SalaryBankCode: setting?.SalaryBankCode,
            SalaryBankName: salaryBank?.FullName,
            SalaryBankBranch: setting?.SalaryBankBranch,
            SalaryAccountNo: setting?.SalaryAccountNo,
            SalaryAccountName: setting?.SalaryAccountName,
            SalaryCustomerCif: setting?.SalaryCustomerCif,
            SalaryContactPhone: setting?.SalaryContactPhone,
            BhxhUnitCode: setting?.BhxhUnitCode,
            BhxhAgencyCode: setting?.BhxhAgencyCode,
            BhxhAgencyName: setting?.BhxhAgencyName,
            BhxhPayerBankCode: setting?.BhxhPayerBankCode,
            BhxhPayerBankName: payerBank?.FullName,
            BhxhPayerBankBranch: setting?.BhxhPayerBankBranch,
            BhxhPayerAccountNo: setting?.BhxhPayerAccountNo,
            BhxhBeneficiaryAccountNo: setting?.BhxhBeneficiaryAccountNo,
            BhxhBeneficiaryBankName: setting?.BhxhBeneficiaryBankName,
            RemittanceContentPreview: content,
            RemittanceContentError: contentError,
            MissingForSalaryOrder: PaymentSettingRequirements.ForSalaryOrder(setting),
            MissingForBhxhRemittance: PaymentSettingRequirements.ForBhxhRemittance(setting),
            UpdatedAt: setting?.UpdatedAt ?? setting?.CreatedAt);
    }

    /// <summary>
    /// Dựng thử nội dung chuyển khoản để kế toán đối chiếu ngay trên màn hình cấu hình.
    /// Mã sai cú pháp thì builder ném — ở đây đó là thông tin cần hiển thị, không phải lỗi 500.
    /// </summary>
    private static (string? Content, string? Error) BuildRemittanceContent(CompanyPaymentSetting? setting)
    {
        try
        {
            return (BhxhRemittanceContentBuilder.Build(setting?.BhxhUnitCode, setting?.BhxhAgencyCode), null);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message);
        }
    }
}
