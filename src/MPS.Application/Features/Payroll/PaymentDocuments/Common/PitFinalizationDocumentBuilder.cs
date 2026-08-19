using System.Globalization;
using System.Text;
using MPS.Application.Common.Models.Compliance;
using EmployeeInformationEntity = MPS.Domain.Entities.EmployeeInformation;
using MPS.Domain.Entities.Payroll;

namespace MPS.Application.Features.Payroll.PaymentDocuments.Common;

/// <summary>Tên + mã số thuế + địa chỉ của tổ chức trả thu nhập, in ở khối [14]-[18].</summary>
public sealed record PitFinalizationEmployer(string Name, string TaxCode, string Address);

/// <summary>
/// Dựng từ điển placeholder cho mẫu 02/QTT-TNCN.
///
/// <para>Mẫu nhúng có 39 ô chỉ tiêu <c>{{k19}}</c>..<c>{{k57}}</c> và 15 ô thông tin. Từ điển
/// LUÔN chứa đủ mọi key — chỉ tiêu doanh nghiệp không nắm được điền chuỗi rỗng, để tờ khai in ra
/// có ô trống thay vì lộ nguyên chữ "{{k21}}" ra bản giấy nộp cho cơ quan thuế.</para>
/// </summary>
public static class PitFinalizationDocumentBuilder
{
    /// <summary>Các chỉ tiêu có trên biểu mẫu.</summary>
    public static readonly int[] IndicatorCodes = Enumerable.Range(19, 57 - 19 + 1).ToArray();

    public static IReadOnlyDictionary<string, string> Build(
        EmployeeInformationEntity employee,
        PitFinalizationEmployer employer,
        int year,
        IReadOnlyList<PayrollRow> yearRows,
        IReadOnlyList<PitBracketSnapshot> monthlyBrackets)
    {
        var figures = PitFinalizationCalculator.Compute(
            yearRows, monthlyBrackets, employee.DependentsNo);

        var months = yearRows
            .Select(r => r.PayrollPeriod.PeriodMonth)
            .DefaultIfEmpty(1)
            .ToList();

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["taxYear"] = year.ToString(CultureInfo.InvariantCulture),
            ["fromMonth"] = months.Min().ToString("00", CultureInfo.InvariantCulture),
            ["toMonth"] = months.Max().ToString("00", CultureInfo.InvariantCulture),

            ["taxpayerName"] = employee.FullName,
            ["taxpayerTaxCode"] = employee.TaxNo ?? employee.CitizenId,
            ["taxpayerAddress"] = employee.PermanentAddress,
            ["taxpayerWard"] = string.Empty,        // hồ sơ NV không tách phường/xã
            ["taxpayerProvince"] = string.Empty,
            ["taxpayerPhone"] = employee.PhoneNo ?? string.Empty,
            ["taxpayerEmail"] = employee.Email ?? string.Empty,

            ["employerName"] = employer.Name,
            ["employerTaxCode"] = employer.TaxCode,
            ["employerAddress"] = employer.Address,
            ["employerWard"] = string.Empty,
            ["employerProvince"] = string.Empty,
        };

        foreach (var code in IndicatorCodes)
        {
            values["k" + code] = FormatIndicator(code, figures[code]);
        }

        return values;
    }

    /// <summary>
    /// Chỉ tiêu [27] là SỐ NGƯỜI nên in số nguyên; còn lại là tiền, in phân cách hàng nghìn.
    /// null ⇒ ô trống (doanh nghiệp không có căn cứ để khai thay người lao động).
    /// </summary>
    private static string FormatIndicator(int code, decimal? value)
    {
        if (value is null) return string.Empty;

        return code == 27
            ? value.Value.ToString("0", CultureInfo.InvariantCulture)
            : value.Value.ToString("#,##0", CultureInfo.InvariantCulture);
    }

    /// <summary>Tên file .docx cho một NV, bỏ dấu để an toàn khi giải nén trên mọi hệ điều hành.</summary>
    public static string BuildFileName(EmployeeInformationEntity employee, string? employeeCode, int year)
    {
        var slug = Slugify(employee.FullName);
        var prefix = string.IsNullOrWhiteSpace(employeeCode) ? string.Empty : employeeCode + "-";

        return $"02-QTT-TNCN-{year}-{prefix}{slug}.docx";
    }

    private static string Slugify(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark) continue;

            if (ch is 'đ' or 'Đ') sb.Append(ch == 'đ' ? 'd' : 'D');
            else if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (char.IsWhiteSpace(ch) || ch == '-') sb.Append('-');
        }

        var slug = sb.ToString().Normalize(NormalizationForm.FormC).Trim('-').ToLowerInvariant();
        while (slug.Contains("--")) slug = slug.Replace("--", "-");

        return slug.Length == 0 ? "nhan-vien" : slug;
    }
}
