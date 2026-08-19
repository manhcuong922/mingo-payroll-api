using System.Text.RegularExpressions;

namespace MPS.Application.Common.Payroll;

/// <summary>
/// Sinh nội dung chuyển khoản cho ủy nhiệm chi nộp BHXH/BHYT/BHTN theo cấu trúc bắt buộc
/// tại Công văn 1995/BHXH-TCKT ngày 30/6/2023 của BHXH Việt Nam:
///
/// <code>+BHXH+103+00+&lt;Mã đơn vị&gt;+&lt;Mã cơ quan BHXH&gt;+dong BHXH+</code>
///
/// <para>Ví dụ trong công văn: <c>+BHXH+103+00+TG14475+00107+dong BHXH+</c>.</para>
///
/// <para>Cấu trúc này chỉ bắt buộc khi LẬP ỦY NHIỆM CHI thủ công; nộp qua ứng dụng
/// Mobile/Internet Banking thì ngân hàng tự điền thông tin.</para>
///
/// <para>⚠️ Sai cú pháp không làm giao dịch thất bại — tiền vẫn chuyển đi, nhưng cơ quan BHXH
/// không đối soát tự động được, phải xử lý tay và quá trình đóng của NLĐ bị treo. Vì vậy toàn bộ
/// input sai đều NÉM thay vì tự đoán/sửa.</para>
/// </summary>
public static class BhxhRemittanceContentBuilder
{
    /// <summary>Mã loại hình thu cố định của BHXH.</summary>
    private const string CollectionTypePrefix = "+BHXH+103+00+";

    private const string Suffix = "+dong BHXH+";

    /// <summary>
    /// Mã đơn vị và mã cơ quan BHXH chỉ gồm chữ và số. Cấm mọi ký tự khác — nhất là dấu '+'
    /// vốn là ký tự phân tách của chính cú pháp, lọt vào sẽ làm lệch số trường khi BHXH tách chuỗi.
    /// </summary>
    private static readonly Regex CodePattern = new("^[A-Z0-9]+$", RegexOptions.Compiled);

    public static string Build(string? bhxhUnitCode, string? bhxhAgencyCode)
    {
        var unitCode = Normalize(bhxhUnitCode, nameof(bhxhUnitCode), "mã đơn vị tham gia BHXH");
        var agencyCode = Normalize(bhxhAgencyCode, nameof(bhxhAgencyCode), "mã cơ quan BHXH quản lý thu");

        return $"{CollectionTypePrefix}{unitCode}+{agencyCode}{Suffix}";
    }

    private static string Normalize(string? raw, string paramName, string label)
    {
        var value = raw?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException(
                $"Chưa khai {label} trong cấu hình chi trả — không lập được ủy nhiệm chi nộp BHXH.",
                paramName);
        }

        if (!CodePattern.IsMatch(value))
        {
            throw new ArgumentException(
                $"{label} chỉ được gồm chữ và số (nhận được '{value}'). Ký tự lạ sẽ phá cấu trúc " +
                "nội dung nộp tiền theo Công văn 1995/BHXH-TCKT.",
                paramName);
        }

        return value;
    }
}
