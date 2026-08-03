using FluentValidation;
using MPS.Application.Common.Interfaces;

namespace MPS.Application.Features.SalaryGrade.Validators;

/// <summary>
/// Bậc lương KHÔNG được thấp hơn lương tối thiểu VÙNG đang áp dụng (Điều 90 k.2 + Điều 91 BLLĐ
/// 2019: "mức lương theo công việc hoặc chức danh không được thấp hơn mức lương tối thiểu").
///
/// <para><b>Lỗi gốc</b>: hai validator Create/Update chỉ có <c>GreaterThan(0)</c>, nên khai được
/// bậc lương 1.000đ/tháng trong khi lương tối thiểu vùng I là 5.310.000đ. Bậc lương là nguồn của
/// <c>Contract.SalaryAmountSnapshot</c> ⇒ con số dưới luật chảy thẳng vào hợp đồng, nền đóng BHXH
/// và bảng lương mà không một bước nào chặn lại.</para>
///
/// <para><b>Vùng nào</b>: hệ thống dùng cho MỘT công ty nên phiên bản lương tối thiểu có sẵn
/// <c>AppliedRegionCode</c> — vùng công ty đang đóng. Không cần hỏi vùng theo từng bậc lương.</para>
///
/// <para><b>Chỉ áp cho VND</b>: bậc lương khai bằng ngoại tệ không so trực tiếp được với mức tối
/// thiểu VND (cần tỷ giá tại thời điểm chi trả — ngoài phạm vi master data).</para>
/// </summary>
public static class MinimumWageRule
{
    private const string VndCurrency = "VND";
    private const string BaseSalaryProperty = "BaseSalary";

    /// <summary>
    /// Gắn rule "không dưới lương tối thiểu vùng" cho <paramref name="validator"/>.
    /// Dùng chung cho cả nhánh tạo mới và nhánh sửa để hai đường không lệch luật.
    /// </summary>
    public static void AddMinimumWageRule<T>(
        AbstractValidator<T> validator,
        IComplianceLookupService compliance,
        Func<T, decimal> baseSalarySelector,
        Func<T, string?> currencySelector)
    {
        validator.RuleFor(command => command).CustomAsync(async (command, context, ct) =>
        {
            if (!IsVnd(currencySelector(command))) return;

            var floor = await TryGetAppliedMinimumWageAsync(compliance, ct);
            if (floor is null) return;

            var baseSalary = baseSalarySelector(command);
            if (baseSalary >= floor.Value) return;

            context.AddFailure(
                BaseSalaryProperty,
                $"BaseSalary ({baseSalary:N0} đ) không được thấp hơn lương tối thiểu vùng đang áp dụng "
                + $"({floor.Value:N0} đ/tháng) — Điều 90 BLLĐ 2019.");
        });
    }

    private static bool IsVnd(string? currency) =>
        string.IsNullOrWhiteSpace(currency)
        || string.Equals(currency.Trim(), VndCurrency, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Trả null khi CHƯA có phiên bản lương tối thiểu nào được kích hoạt — khi đó bỏ qua rule thay
    /// vì ném 500 ở tầng validator. Thiếu dữ liệu compliance vốn đã là lỗi chặn ồn ào ở luồng tính
    /// lương (<see cref="IComplianceLookupService.GetActiveMinimumWageAsync"/> throw), không nên
    /// kéo theo việc khoá luôn cả màn hình cấu hình master data.
    /// </summary>
    private static async Task<decimal?> TryGetAppliedMinimumWageAsync(
        IComplianceLookupService compliance,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await compliance.GetActiveMinimumWageAsync(ct);
            return snapshot.GetAppliedAmount();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
