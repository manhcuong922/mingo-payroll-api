using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities.Payroll.Banking;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.CompanyPaymentSettings.Commands;

/// <summary>
/// Ghi đè toàn bộ cấu hình chi trả (bản ghi singleton).
///
/// <para>Cố ý là FULL-REPLACE chứ không merge từng field như hồ sơ nhân sự: đây là một form duy
/// nhất, luôn được nạp đầy rồi lưu cả cụm, nên "không gửi" chỉ có thể nghĩa là "xoá trắng ô đó".
/// Merge ở đây sẽ khiến kế toán không bao giờ xoá được một ô đã khai nhầm.</para>
/// </summary>
public sealed record UpdateCompanyPaymentSettingCommand(
    string? SalaryBankCode,
    string? SalaryBankBranch,
    string? SalaryAccountNo,
    string? SalaryAccountName,
    string? SalaryCustomerCif,
    string? SalaryContactPhone,
    string? BhxhUnitCode,
    string? BhxhAgencyCode,
    string? BhxhAgencyName,
    string? BhxhPayerBankCode,
    string? BhxhPayerBankBranch,
    string? BhxhPayerAccountNo,
    string? BhxhBeneficiaryAccountNo,
    string? BhxhBeneficiaryBankName) : IRequest<Unit>;

public sealed class UpdateCompanyPaymentSettingCommandHandler
    : IRequestHandler<UpdateCompanyPaymentSettingCommand, Unit>
{
    private readonly ICompanyPaymentSettingRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public UpdateCompanyPaymentSettingCommandHandler(
        ICompanyPaymentSettingRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateCompanyPaymentSettingCommand request, CancellationToken ct)
    {
        var existing = await _repository.GetAsync(ct);
        var now = DateTime.UtcNow;

        var setting = new CompanyPaymentSetting
        {
            SalaryBankCode = Clean(request.SalaryBankCode),
            SalaryBankBranch = Clean(request.SalaryBankBranch),
            SalaryAccountNo = Clean(request.SalaryAccountNo),
            SalaryAccountName = Clean(request.SalaryAccountName),
            SalaryCustomerCif = Clean(request.SalaryCustomerCif),
            SalaryContactPhone = Clean(request.SalaryContactPhone),

            BhxhUnitCode = CleanCode(request.BhxhUnitCode),
            BhxhAgencyCode = CleanCode(request.BhxhAgencyCode),
            BhxhAgencyName = Clean(request.BhxhAgencyName),
            BhxhPayerBankCode = Clean(request.BhxhPayerBankCode),
            BhxhPayerBankBranch = Clean(request.BhxhPayerBankBranch),
            BhxhPayerAccountNo = Clean(request.BhxhPayerAccountNo),
            BhxhBeneficiaryAccountNo = Clean(request.BhxhBeneficiaryAccountNo),
            BhxhBeneficiaryBankName = Clean(request.BhxhBeneficiaryBankName),

            CreatedAt = existing?.CreatedAt ?? now,
            CreatedByUserId = existing?.CreatedByUserId ?? _currentUser.UserId,
            UpdatedAt = now,
            UpdatedByUserId = _currentUser.UserId,
        };

        await _repository.UpsertAsync(setting, ct);
        return Unit.Value;
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Mã BHXH luôn lưu VIẾT HOA — đó là dạng đi vào nội dung chuyển khoản theo Công văn
    /// 1995/BHXH-TCKT, chuẩn hoá tại chỗ ghi để chỗ đọc không phải nhớ.
    /// </summary>
    private static string? CleanCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
