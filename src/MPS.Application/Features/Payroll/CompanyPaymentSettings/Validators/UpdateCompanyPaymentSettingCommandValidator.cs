using System.Text.RegularExpressions;
using FluentValidation;
using MPS.Application.Features.Payroll.CompanyPaymentSettings.Commands;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.CompanyPaymentSettings.Validators;

/// <summary>
/// Cấu hình cho phép khai DẦN (mọi field nullable), nên ở đây không có rule "bắt buộc" nào —
/// việc thiếu field được báo ở màn hình cấu hình và chặn ở lệnh xuất
/// (<c>PaymentSettingRequirements</c>).
///
/// <para>Chỉ chốt hai thứ mà sai là hỏng chứng từ về sau: mã ngân hàng phải có trong danh mục,
/// và mã BHXH phải đúng dạng chữ-số của Công văn 1995/BHXH-TCKT.</para>
/// </summary>
public sealed class UpdateCompanyPaymentSettingCommandValidator
    : AbstractValidator<UpdateCompanyPaymentSettingCommand>
{
    /// <summary>Cấm mọi ký tự ngoài chữ và số — nhất là '+' vốn là ký tự phân tách của cú pháp.</summary>
    private static readonly Regex BhxhCodePattern = new("^[A-Za-z0-9]+$", RegexOptions.Compiled);

    public UpdateCompanyPaymentSettingCommandValidator(ICompanyPaymentSettingRepository repository)
    {
        RuleFor(x => x.SalaryBankCode)
            .MaximumLength(50)
            .MustAsync((code, ct) => BankExistsAsync(repository, code, ct))
            .WithMessage("Ngân hàng trả lương không có trong danh mục ngân hàng của lệnh chi.");

        RuleFor(x => x.BhxhPayerBankCode)
            .MaximumLength(50)
            .MustAsync((code, ct) => BankExistsAsync(repository, code, ct))
            .WithMessage("Ngân hàng nộp BHXH không có trong danh mục ngân hàng của lệnh chi.");

        RuleFor(x => x.BhxhUnitCode)
            .MaximumLength(50)
            .Must(BeValidBhxhCode)
            .WithMessage("Mã đơn vị tham gia BHXH chỉ được gồm chữ và số.");

        RuleFor(x => x.BhxhAgencyCode)
            .MaximumLength(50)
            .Must(BeValidBhxhCode)
            .WithMessage("Mã cơ quan BHXH quản lý thu chỉ được gồm chữ và số.");

        RuleFor(x => x.SalaryBankBranch).MaximumLength(200);
        RuleFor(x => x.SalaryAccountNo).MaximumLength(50);
        RuleFor(x => x.SalaryAccountName).MaximumLength(200);
        RuleFor(x => x.SalaryCustomerCif).MaximumLength(50);
        RuleFor(x => x.SalaryContactPhone).MaximumLength(20);
        RuleFor(x => x.BhxhAgencyName).MaximumLength(200);
        RuleFor(x => x.BhxhPayerBankBranch).MaximumLength(200);
        RuleFor(x => x.BhxhPayerAccountNo).MaximumLength(50);
        RuleFor(x => x.BhxhBeneficiaryAccountNo).MaximumLength(50);
        RuleFor(x => x.BhxhBeneficiaryBankName).MaximumLength(200);
    }

    private static async Task<bool> BankExistsAsync(
        ICompanyPaymentSettingRepository repository, string? code, CancellationToken ct)
        => string.IsNullOrWhiteSpace(code) || await repository.FindBankAsync(code.Trim(), ct) is not null;

    private static bool BeValidBhxhCode(string? code)
        => string.IsNullOrWhiteSpace(code) || BhxhCodePattern.IsMatch(code.Trim());
}
