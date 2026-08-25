using System.Linq.Expressions;
using FluentValidation;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.EmployeeInformation.Validators;

/// <summary>
/// Rule dùng chung cho khối tài khoản ngân hàng của hồ sơ NV (tạo và sửa dùng y hệt nhau).
///
/// <para>Mã ngân hàng và mã tỉnh/TP KHÔNG có FK ở DB — danh mục do ngân hàng phát hành nên có thể
/// bị gỡ dòng, khoá cứng sẽ chặn cả việc cập nhật danh mục. Vì vậy tính hợp lệ chốt tại đây:
/// một mã sai không làm hỏng hồ sơ NV, nhưng đến kỳ chi lương thì VPBank trả lại CẢ lệnh chi
/// chứ không riêng dòng đó.</para>
///
/// <para>Cố ý KHÔNG bắt buộc khai chi nhánh/tỉnh ở đây: hồ sơ nhân sự phải lưu được kể cả khi
/// chưa đủ thông tin ngân hàng. Việc thiếu được báo ở màn hình xuất chứng từ
/// (<c>SalaryPaymentPlanBuilder</c> liệt kê NV bị loại kèm lý do).</para>
/// </summary>
internal static class EmployeeBankAccountRules
{
    public static void Apply<TCommand>(
        AbstractValidator<TCommand> validator,
        ICompanyPaymentSettingRepository bankingRepository,
        Expression<Func<TCommand, string?>> bankCode,
        Expression<Func<TCommand, string?>> bankBranch,
        Expression<Func<TCommand, string?>> bankProvinceCode)
    {
        validator.RuleFor(bankCode)
            .MaximumLength(50).WithMessage("Mã ngân hàng không được vượt quá 50 ký tự.")
            .MustAsync(async (code, ct) =>
                string.IsNullOrWhiteSpace(code)
                || await bankingRepository.FindBankAsync(code.Trim(), ct) is not null)
            .WithMessage("Ngân hàng không có trong danh mục ngân hàng dùng cho lệnh chi lương.");

        validator.RuleFor(bankBranch)
            .MaximumLength(200).WithMessage("Tên chi nhánh không được vượt quá 200 ký tự.");

        validator.RuleFor(bankProvinceCode)
            .MaximumLength(50).WithMessage("Mã tỉnh/thành phố không được vượt quá 50 ký tự.")
            .MustAsync(async (code, ct) =>
                string.IsNullOrWhiteSpace(code)
                || await bankingRepository.FindProvinceAsync(code.Trim(), ct) is not null)
            .WithMessage("Tỉnh/thành phố không có trong danh mục của lệnh chi lương.");
    }
}
