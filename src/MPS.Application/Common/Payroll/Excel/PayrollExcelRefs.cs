using MPS.Application.Common.Models.Compliance;

namespace MPS.Application.Common.Payroll.Excel;

/// <summary>
/// Bảng tra cứu để dịch 1 nút AST thành tham chiếu Excel: cột → ô cùng dòng (vd <c>O13</c>),
/// field/param → named range (vd <c>GIA_COM</c>) hoặc ô cột phụ trợ (vd <c>P13</c>).
/// Tập text dùng để chọn <c>EXACT()</c> thay <c>=</c> khi so sánh chuỗi.
/// </summary>
public sealed record PayrollExcelRefs(
    IReadOnlyDictionary<string, string> ColumnRefs,
    IReadOnlyDictionary<string, string> FieldRefs,
    IReadOnlyDictionary<string, string> ParamRefs,
    IReadOnlySet<string> TextColumns,
    IReadOnlySet<string> TextFields,
    IReadOnlyList<PitBracketSnapshot> PitBrackets);
