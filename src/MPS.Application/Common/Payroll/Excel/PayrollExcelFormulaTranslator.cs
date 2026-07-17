using System.Globalization;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Common.Payroll.Excel;

/// <summary>
/// Dịch AST <see cref="FormulaNode"/> thành công thức Excel THẬT (bấm vào ô là thấy, sửa input là
/// Excel tự tính lại) — khác chuỗi <c>Explanation</c> mà UI đang hover.
///
/// Nguyên tắc: file Excel phải ra ĐÚNG số của engine. Mọi chỗ ngữ nghĩa engine ≠ Excel đều phải
/// bù, nếu không mở file lên là số lệch (workbook bật <c>fullCalcOnLoad</c> nên Excel tính lại ngay):
///   1. Chia 0: engine trả 0, Excel trả <c>#DIV/0!</c> → bọc <c>IFERROR(…,0)</c>.
///   2. So sánh chuỗi: engine dùng Ordinal (phân biệt hoa/thường), Excel <c>=</c> thì không → <c>EXACT()</c>.
///   3. Nút text trong ngữ cảnh số: engine cho Value = 0, Excel cho <c>#VALUE!</c> → dịch thành 0.
///   4. Điều kiện IF không phải phép so sánh: engine dùng "khác 0 / chuỗi không rỗng" → dựng lại rõ ràng.
///   5. Hàm nội bộ (BH_CO_TRAN/THUNHAP_PHANBO/THUE_LUYTIEN) không có trong Excel → bung ra công thức gốc.
///   6. Làm tròn của cột nằm NGOÀI ast (engine áp sau khi tính) → bọc qua <see cref="Wrap"/>.
/// Trường hợp không thể dịch trung thực thì NÉM LỖI, không phát ra công thức ra số khác engine.
/// </summary>
public static class PayrollExcelFormulaTranslator
{
    /// <summary>Dịch AST → thân công thức Excel (chưa có dấu <c>=</c>, chưa bọc làm tròn của cột).</summary>
    public static string Translate(FormulaNode node, PayrollExcelRefs refs) => Numeric(node, refs);

    /// <summary>
    /// Bọc làm tròn của cột — phải soi gương <see cref="PayrollRounding.ApplyForColumn"/>: ngoài
    /// <paramref name="rounding"/>, cột TIỀN không khai báo làm tròn vẫn được engine làm tròn về
    /// đồng nguyên (khử dư số học từ phép chia công). Bỏ bước này là Excel ra số lẻ ≠ hệ thống.
    /// </summary>
    public static string Wrap(string expression, RoundingMode rounding, ColumnDataType dataType) => rounding switch
    {
        RoundingMode.ToThreeDecimals => $"ROUND({expression},3)",
        RoundingMode.ToThousand => $"ROUND({expression},-3)",
        _ => dataType == ColumnDataType.Money ? $"ROUND({expression},0)" : expression,
    };

    // ===================== Ngữ cảnh =====================

    /// <summary>Ngữ cảnh SỐ: engine cho nút text giá trị 0 (xem FormulaEvaluator.TextNode/ToFields).</summary>
    private static string Numeric(FormulaNode node, PayrollExcelRefs refs) =>
        IsText(node, refs) ? "0" : Raw(node, refs);

    /// <summary>Ngữ cảnh LOGIC: dựng lại đúng quy tắc IsTruthy của engine (khác 0 / chuỗi không rỗng).</summary>
    private static string Boolean(FormulaNode node, PayrollExcelRefs refs)
    {
        if (node.Type == FormulaNodeType.Compare) return Raw(node, refs);
        if (IsText(node, refs)) return $"(LEN({Raw(node, refs)})>0)";
        return $"({Raw(node, refs)}<>0)";
    }

    private static string Raw(FormulaNode node, PayrollExcelRefs refs) => node.Type switch
    {
        FormulaNodeType.Number => Number(node.Number ?? 0m),
        FormulaNodeType.Text => Quote(node.Text ?? string.Empty),
        FormulaNodeType.Column => Lookup(refs.ColumnRefs, node.Text, "cột"),
        FormulaNodeType.Field => Lookup(refs.FieldRefs, node.Text, "field"),
        FormulaNodeType.Binary => Binary(node, refs),
        FormulaNodeType.Compare => Compare(node, refs),
        FormulaNodeType.If =>
            $"IF({Boolean(node.Args[0], refs)},{Numeric(node.Args[1], refs)},{Numeric(node.Args[2], refs)})",
        FormulaNodeType.Function => Function(node, refs),
        _ => throw new InvalidOperationException($"Không dịch được nút công thức sang Excel: {node.Type}."),
    };

    // ===================== Nút lá =====================

    private static string Lookup(IReadOnlyDictionary<string, string> map, string? key, string kind)
    {
        if (string.IsNullOrEmpty(key))
            throw new InvalidOperationException($"Nút {kind} thiếu khoá tham chiếu.");
        if (!map.TryGetValue(key, out var reference))
            throw new InvalidOperationException($"Không có ô Excel cho {kind} '{key}'.");
        return reference;
    }

    /// <summary>
    /// Bỏ số 0 thừa ở đuôi để công thức dễ đọc: phép nhân decimal giữ nguyên scale nên
    /// 5.000.000 × 0,05 ra "250000.00". Giá trị không đổi, chỉ khác cách in.
    /// </summary>
    private static string Number(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);

    private static string Quote(string text) => $"\"{text.Replace("\"", "\"\"")}\"";

    private static bool IsText(FormulaNode node, PayrollExcelRefs refs) => node.Type switch
    {
        FormulaNodeType.Text => true,
        FormulaNodeType.Column => node.Text is not null && refs.TextColumns.Contains(node.Text),
        FormulaNodeType.Field => node.Text is not null && refs.TextFields.Contains(node.Text),
        _ => false,
    };

    // ===================== Toán tử =====================

    private static string Binary(FormulaNode node, PayrollExcelRefs refs)
    {
        var l = Numeric(node.Args[0], refs);
        var r = Numeric(node.Args[1], refs);
        return node.Text switch
        {
            "+" or "-" or "*" => $"({l}{node.Text}{r})",
            // Engine: chia cho 0 → 0. Excel: #DIV/0! → IFERROR giữ đúng ngữ nghĩa engine.
            "/" => $"IFERROR({l}/{r},0)",
            _ => throw new InvalidOperationException($"Toán tử không dịch được sang Excel: {node.Text}."),
        };
    }

    private static string Compare(FormulaNode node, PayrollExcelRefs refs)
    {
        var (left, right) = (node.Args[0], node.Args[1]);

        if (IsText(left, refs) || IsText(right, refs))
        {
            // Engine so sánh chuỗi Ordinal ⇒ EXACT (Excel "=" không phân biệt hoa/thường).
            // Engine trả false cho mọi toán tử khác "="/"<>" trên chuỗi.
            var l = TextSide(left, refs);
            var r = TextSide(right, refs);
            return node.Text switch
            {
                "=" => $"EXACT({l},{r})",
                "<>" => $"NOT(EXACT({l},{r}))",
                _ => "FALSE",
            };
        }

        return node.Text switch
        {
            "=" or "<>" or "<" or "<=" or ">" or ">=" => $"({Numeric(left, refs)}{node.Text}{Numeric(right, refs)})",
            _ => throw new InvalidOperationException($"Toán tử so sánh không dịch được sang Excel: {node.Text}."),
        };
    }

    /// <summary>
    /// Vế của phép so sánh chuỗi. Engine quy đổi vế SỐ sang chuỗi bằng định dạng VN
    /// (<c>5.000.000</c>) — hằng số thì dịch được y hệt, còn ô/field thì Excel định dạng theo
    /// locale máy người dùng nên không thể bảo đảm trùng → ném lỗi thay vì ra số sai.
    /// </summary>
    private static string TextSide(FormulaNode node, PayrollExcelRefs refs)
    {
        if (IsText(node, refs)) return Raw(node, refs);
        if (node.Type == FormulaNodeType.Number) return Quote(PayrollNumberFormat.Format(node.Number ?? 0m));

        throw new InvalidOperationException(
            "Không dịch được sang Excel: công thức so sánh chuỗi với một ô số. " +
            "Hãy so sánh chuỗi với chuỗi, hoặc số với số.");
    }

    // ===================== Hàm =====================

    private static string Function(FormulaNode node, PayrollExcelRefs refs)
    {
        var args = node.Args.Select(a => Numeric(a, refs)).ToList();
        return node.Text switch
        {
            // Engine trả 0 khi không có tham số; Excel thì SUM() là lỗi cú pháp.
            PayrollFunctions.Sum => args.Count == 0 ? "0" : $"SUM({string.Join(",", args)})",
            PayrollFunctions.Min => args.Count == 0 ? "0" : $"MIN({string.Join(",", args)})",
            PayrollFunctions.Max => args.Count == 0 ? "0" : $"MAX({string.Join(",", args)})",
            PayrollFunctions.Abs => $"ABS({args[0]})",
            PayrollFunctions.Round => $"ROUND({args[0]},{args[1]})",
            PayrollFunctions.InsuranceWithCap => InsuranceWithCap(args),
            PayrollFunctions.IncomeProration => IncomeProration(args),
            PayrollFunctions.ProgressiveTax => ProgressiveTax(args[0], refs.PitBrackets),
            _ => throw new InvalidOperationException($"Hàm không dịch được sang Excel: {node.Text}."),
        };
    }

    /// <summary>BH_CO_TRAN(base, rate, cap) → <c>IF(cap&gt;0,MIN(base,cap),base)*rate</c>. cap ≤ 0 = không trần.</summary>
    private static string InsuranceWithCap(IReadOnlyList<string> a)
    {
        var (@base, rate, cap) = (a[0], a[1], a[2]);
        return $"(IF({cap}>0,MIN({@base},{cap}),{@base})*{rate})";
    }

    /// <summary>
    /// THUNHAP_PHANBO(base,p,q,r,j,m,l,n,extra) → công thức lõi cột Y (đặc tả §3-Khối E):
    /// <c>(base−q−r)/j×m + (base−p−r)/j×l + (r/j)×n + extra</c>, guard <c>j=0</c> như engine.
    /// </summary>
    private static string IncomeProration(IReadOnlyList<string> a)
    {
        var (@base, p, q, r) = (a[0], a[1], a[2], a[3]);
        var (j, m, l, n, extra) = (a[4], a[5], a[6], a[7], a[8]);
        var body =
            $"((({@base}-{q}-{r})/{j}*{m})" +
            $"+(({@base}-{p}-{r})/{j}*{l})" +
            $"+(({r}/{j})*{n})" +
            $"+{extra})";
        return $"IF({j}=0,{extra},{body})";
    }

    /// <summary>
    /// THUE_LUYTIEN(x) → IF lồng sinh từ biểu thuế SNAPSHOT của kỳ (không hardcode bậc).
    /// Mỗi bậc: <c>thuế luỹ kế tới cận dưới + (x − cận dưới) × thuế suất</c> — khớp cách engine cộng từng phần.
    /// </summary>
    private static string ProgressiveTax(string x, IReadOnlyList<PitBracketSnapshot> brackets)
    {
        if (brackets.Count == 0) return "0";

        var ordered = brackets.OrderBy(b => b.Order).ToList();
        for (var i = 0; i < ordered.Count - 1; i++)
        {
            if (ordered[i].UpperBound is null)
                throw new InvalidOperationException(
                    $"Biểu thuế của kỳ hỏng: bậc {ordered[i].Order} không có cận trên nhưng chưa phải bậc cuối.");
        }

        var cumulative = new decimal[ordered.Count];
        for (var i = 1; i < ordered.Count; i++)
        {
            var prev = ordered[i - 1];
            var width = (prev.UpperBound ?? prev.LowerBound) - prev.LowerBound;
            cumulative[i] = cumulative[i - 1] + width * prev.Rate;
        }

        string Segment(int i)
        {
            var b = ordered[i];
            var rate = Number(b.Rate);
            if (cumulative[i] == 0m && b.LowerBound == 0m) return $"{x}*{rate}";
            return $"({Number(cumulative[i])}+({x}-{Number(b.LowerBound)})*{rate})";
        }

        // Bậc cuối (không cận trên) là nhánh else; dựng ngược từ trong ra ngoài.
        var expr = Segment(ordered.Count - 1);
        for (var i = ordered.Count - 2; i >= 0; i--)
            expr = $"IF({x}<={Number(ordered[i].UpperBound!.Value)},{Segment(i)},{expr})";

        return $"IF({x}<=0,0,{expr})";
    }
}
