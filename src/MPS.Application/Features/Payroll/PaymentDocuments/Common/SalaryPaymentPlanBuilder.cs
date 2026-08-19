using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Features.Payroll.PaymentDocuments.Common;

/// <summary>
/// Quyết định ai vào lệnh chi lương, xếp vào bảng nào, ai bị loại và vì sao.
///
/// <para>Thuần hàm (không I/O) để test được toàn bộ luật nghiệp vụ — đây là chỗ quyết định
/// tiền của ai được chuyển, nên phải kiểm được từng nhánh.</para>
/// </summary>
public static class SalaryPaymentPlanBuilder
{
    public const string ReasonMissingAccount = "Chưa khai số tài khoản ngân hàng";
    public const string ReasonMissingBankCode = "Chưa khai ngân hàng";
    public const string ReasonMissingBranchInfo =
        "Tài khoản ở ngân hàng khác nhưng chưa khai chi nhánh / tỉnh thành";
    public const string ReasonNonPositiveNet = "Thực nhận bằng 0 hoặc âm";

    /// <param name="rows">Các dòng của kỳ lương (cả dòng tổng, sẽ bị lọc bỏ).</param>
    /// <param name="inHouseBankCode">Mã ngân hàng chủ quản trả lương của công ty.</param>
    /// <param name="provinceFormNames">Tra mã tỉnh → tên ASCII ghi trên lệnh chi.</param>
    public static SalaryPaymentPlan Build(
        IEnumerable<PayrollRow> rows,
        string inHouseBankCode,
        IReadOnlyDictionary<string, string> provinceFormNames)
    {
        var inHouse = new List<SalaryPaymentBeneficiary>();
        var others = new List<SalaryPaymentBeneficiary>();
        var excluded = new List<SalaryPaymentExclusion>();

        var employeeRows = rows
            .Where(r => r.RowType == PayrollRowType.Employee && r.EmployeeInformation is not null)
            .OrderBy(r => r.DisplayOrder);

        foreach (var row in employeeRows)
        {
            var employee = row.EmployeeInformation!;
            var code = employee.User?.EmployeeCode ?? string.Empty;
            var amount = PayrollCellReader.ValueOrZero(row, PayrollColumnCodes.ThucNhan);

            var reason = FindExclusionReason(employee, amount, inHouseBankCode);
            if (reason is not null)
            {
                excluded.Add(new SalaryPaymentExclusion(code, employee.FullName, amount, reason));
                continue;
            }

            var beneficiary = new SalaryPaymentBeneficiary(
                EmployeeCode: code,
                FullName: employee.FullName,
                AccountNo: employee.BankNumber!.Trim(),
                Amount: amount,
                Address: employee.ContactAddress ?? employee.PermanentAddress,
                BankCode: employee.BankCode!.Trim(),
                BankBranch: employee.BankBranch,
                ProvinceFormName: LookupProvince(employee.BankProvinceCode, provinceFormNames));

            if (string.Equals(beneficiary.BankCode, inHouseBankCode, StringComparison.OrdinalIgnoreCase))
            {
                inHouse.Add(beneficiary);
            }
            else
            {
                others.Add(beneficiary);
            }
        }

        return new SalaryPaymentPlan(inHouse, others, excluded);
    }

    private static string? FindExclusionReason(
        Domain.Entities.EmployeeInformation employee, decimal amount, string inHouseBankCode)
    {
        // Thứ tự kiểm có chủ ý: báo lý do "không có tiền để chuyển" trước lý do "thiếu hồ sơ",
        // vì NV thực nhận 0 thì việc thiếu số tài khoản không phải vấn đề cần xử lý.
        if (amount <= 0) return ReasonNonPositiveNet;
        if (string.IsNullOrWhiteSpace(employee.BankNumber)) return ReasonMissingAccount;
        if (string.IsNullOrWhiteSpace(employee.BankCode)) return ReasonMissingBankCode;

        var isOtherBank = !string.Equals(
            employee.BankCode.Trim(), inHouseBankCode, StringComparison.OrdinalIgnoreCase);

        // Mẫu MB01 bắt buộc khai chi nhánh + tỉnh/TP với NV ở ngân hàng khác; thiếu là
        // VPBank trả lại cả lệnh chứ không riêng dòng đó.
        if (isOtherBank
            && (string.IsNullOrWhiteSpace(employee.BankBranch)
                || string.IsNullOrWhiteSpace(employee.BankProvinceCode)))
        {
            return ReasonMissingBranchInfo;
        }

        return null;
    }

    private static string? LookupProvince(
        string? provinceCode, IReadOnlyDictionary<string, string> provinceFormNames)
    {
        if (string.IsNullOrWhiteSpace(provinceCode)) return null;

        return provinceFormNames.TryGetValue(provinceCode, out var formName) ? formName : null;
    }
}
