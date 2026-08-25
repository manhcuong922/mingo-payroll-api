using System.Text;

namespace MPS.Application.Common.Text;

/// <summary>
/// Đọc số tiền VNĐ thành chữ tiếng Việt cho ô "Số tiền bằng chữ" của ủy nhiệm chi / lệnh chi lương.
///
/// <para>Ngân hàng đối chiếu ô chữ với ô số; lệch nhau là trả lại lệnh — nên hàm này làm tròn
/// về đồng chẵn đúng bằng cách số tiền được ghi ra, không tự ý bỏ phần lẻ.</para>
///
/// <para>Các luật đọc được xử lý: "linh" cho hàng chục bằng 0, "mươi" cho hàng chục ≥ 2,
/// "mốt" cho hàng đơn vị 1 sau "mươi", "lăm" cho hàng đơn vị 5 sau "mươi"/"mười", và
/// "không trăm" khi nhóm 3 chữ số đứng sau một nhóm lớn hơn.</para>
/// </summary>
public static class VietnameseMoneyWords
{
    private static readonly string[] Digits =
        ["không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"];

    /// <summary>Đơn vị của từng nhóm 3 chữ số, từ nhóm thấp nhất lên.</summary>
    private static readonly string[] GroupUnits = ["", " nghìn", " triệu", " tỷ"];

    private const string Currency = " đồng";

    /// <summary>Đọc số tiền kèm đơn vị, VD "Năm tỷ đồng".</summary>
    public static string ToWords(decimal amount) => ToPlainWords(amount) + Currency;

    /// <summary>
    /// Đọc số thành chữ KHÔNG kèm đơn vị tiền — dùng cho ô đếm số lượng như
    /// "Số món (Bằng chữ)" trên lệnh chi lương.
    /// </summary>
    public static string ToPlainWords(decimal amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), amount, "Số tiền trên chứng từ chi trả không thể âm.");
        }

        var rounded = Math.Round(amount, 0, MidpointRounding.AwayFromZero);

        if (rounded == 0) return Capitalize(Digits[0]);

        // Tách thành các nhóm 3 chữ số, nhóm thấp nhất ở index 0.
        var groups = new List<int>();
        var remaining = rounded;
        while (remaining > 0)
        {
            groups.Add((int)(remaining % 1000));
            remaining = Math.Floor(remaining / 1000);
        }

        if (groups.Count > GroupUnits.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), amount, "Số tiền vượt quá đơn vị 'tỷ' mà biểu mẫu hỗ trợ.");
        }

        var sb = new StringBuilder();
        for (var i = groups.Count - 1; i >= 0; i--)
        {
            // Nhóm bằng 0 bỏ hẳn: 1.000.500 đọc là "một triệu năm trăm", không phải
            // "một triệu không trăm năm trăm". Bậc số vẫn rõ nhờ đơn vị nghìn/triệu/tỷ.
            if (groups[i] == 0) continue;

            if (sb.Length > 0) sb.Append(' ');
            sb.Append(ReadGroup(groups[i], isLeadingGroup: sb.Length == 0));
            sb.Append(GroupUnits[i]);
        }

        return Capitalize(sb.ToString());
    }

    /// <param name="isLeadingGroup">
    /// Nhóm đầu tiên được đọc thì bỏ "không trăm" thừa (1.000 = "một nghìn", không phải
    /// "không trăm một nghìn"); các nhóm sau bắt buộc giữ để không mất bậc số.
    /// </param>
    private static string ReadGroup(int group, bool isLeadingGroup)
    {
        var hundreds = group / 100;
        var tens = group % 100 / 10;
        var units = group % 10;

        var sb = new StringBuilder();

        if (hundreds > 0 || !isLeadingGroup)
        {
            sb.Append(Digits[hundreds]).Append(" trăm");
        }

        if (tens == 0)
        {
            if (units > 0)
            {
                // "linh" chỉ xuất hiện khi đã có phần trăm đứng trước.
                if (sb.Length > 0) sb.Append(" linh");
                sb.Append(' ').Append(Digits[units]);
            }
        }
        else if (tens == 1)
        {
            sb.Append(sb.Length > 0 ? " mười" : "mười");
            if (units == 5) sb.Append(" lăm");
            else if (units > 0) sb.Append(' ').Append(Digits[units]);
        }
        else
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(Digits[tens]).Append(" mươi");
            if (units == 1) sb.Append(" mốt");
            else if (units == 5) sb.Append(" lăm");
            else if (units > 0) sb.Append(' ').Append(Digits[units]);
        }

        return sb.ToString().Trim();
    }

    private static string Capitalize(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
