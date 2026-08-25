using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Entities.Payroll;
using FormulaNode = MPS.Application.Common.Payroll.Formula.FormulaNode;
using N = MPS.Application.Common.Payroll.Formula.FormulaNode;
using Cc = MPS.Domain.Constants.PayrollColumnCodes;
using Fk = MPS.Application.Common.Payroll.PayrollFieldKeys;
using Fn = MPS.Domain.Constants.PayrollFunctions;
using TaxFlags = MPS.Domain.Enums.Payroll.TaxFlags;

namespace MPS.Infrastructure.Persistence;

/// <summary>
/// Các công thức pháp luật dùng riêng cho bảng công thức được SEED xuống database.
/// Không thay đổi collector, calculator hay bộ công thức runtime gốc của hệ thống.
/// </summary>
public sealed partial class DatabaseSeeder
{
    private static List<SalaryColumnDefinition> BuildSeedPayrollColumns()
    {
        var columns = DefaultPayrollFormulaSet.BuildColumns();

        ApplySeedTaxFormula(columns);
        ApplySeedMonthlyInsuranceFormula(columns);
        ApplySeedUnionDuesFormula(columns);
        ValidateSeedInsuranceFormulas(columns);

        return columns;
    }

    /// <summary>
    /// Thuế khoán của cá nhân cư trú HĐ dưới 3 tháng/không HĐ và thuế 20% của cá nhân không cư trú
    /// đều khấu trừ trên THU NHẬP CHỊU THUẾ của lần chi trả. Không nhân trên TỔNG THU NHẬP và cũng
    /// không dùng TNTT sau giảm trừ gia cảnh của biểu lũy tiến.
    /// </summary>
    private static void ApplySeedTaxFormula(IReadOnlyCollection<SalaryColumnDefinition> columns)
    {
        var flatRate = N.If(
            N.Cmp(">", N.Field(Fk.ShortTermFlatRate), N.Const(0m)),
            N.Field(Fk.ShortTermFlatRate),
            N.Const(0.10m));

        var flatTax = N.If(
            N.Cmp("<", N.Col(Cc.Tnct), N.Field(Fk.ShortTermTaxExemptIncome)),
            N.Const(0m),
            N.Mul(N.Col(Cc.Tnct), flatRate));

        var formula = N.If(
            N.Cmp("=", N.Col(Cc.FlagThue), N.Literal(TaxFlags.Khoan20)),
            N.Mul(N.Col(Cc.Tnct), N.Const(0.20m)),
            N.If(
                N.Cmp("=", N.Col(Cc.FlagThue), N.Literal(TaxFlags.Khoan10)),
                flatTax,
                N.If(
                    N.Cmp("=", N.Col(Cc.FlagThue), N.Literal(TaxFlags.CamKet)),
                    N.Const(0m),
                    N.Func(Fn.ProgressiveTax, N.Col(Cc.Tntt)))));

        SetFormula(columns, Cc.ThueTncn, formula);
    }

    /// <summary>
    /// Cấu hình seed BHXH/BHYT/BHTN và KPCĐ trên căn cứ đóng tháng đã chặn trần. Seeder bỏ lớp
    /// chia theo công của bộ mặc định cũ; cổng miễn cả tháng có sẵn trong công thức vẫn được giữ nguyên.
    /// </summary>
    private static void ApplySeedMonthlyInsuranceFormula(IReadOnlyCollection<SalaryColumnDefinition> columns)
    {
        var codes = new[]
        {
            Cc.DnBhxh, Cc.NldBhxh, Cc.DnBhyt, Cc.NldBhyt,
            Cc.DnBhtn, Cc.NldBhtn, Cc.Kpcd, Cc.PhiCongDoanVien,
        };

        foreach (var code in codes)
        {
            var column = RequireColumn(columns, code);
            var root = PayrollJson.Deserialize<FormulaNode>(column.FormulaJson!)
                ?? throw new InvalidOperationException($"Không đọc được công thức seed của cột {code}.");

            Visit(root, node =>
            {
                if (IsField(node, Fk.MienBhThieuNgayCong))
                {
                    ReplaceNode(node, SeedInsuranceMonthExemption());
                    return;
                }

                if (node.Type != FormulaNodeType.Function
                    || !string.Equals(node.Text, Fn.InsuranceWithCap, StringComparison.Ordinal)
                    || node.Args.Count != 3)
                    return;

                node.Args[0] = UnwrapAttendanceProration(node.Args[0]);
            });

            column.FormulaJson = PayrollJson.Serialize(root);
        }
    }

    /// <summary>Đoàn phí dùng cùng căn cứ tiền lương đóng BHXH đã chặn trần, không chỉ riêng lương cơ bản.</summary>
    private static void ApplySeedUnionDuesFormula(IReadOnlyCollection<SalaryColumnDefinition> columns)
    {
        var column = RequireColumn(columns, Cc.PhiCongDoanVien);
        var root = PayrollJson.Deserialize<FormulaNode>(column.FormulaJson!)
            ?? throw new InvalidOperationException("Không đọc được công thức seed của cột PHICONGDOANVIEN.");

        Visit(root, node =>
        {
            if (node.Type != FormulaNodeType.Binary || node.Text != "*" || node.Args.Count != 2)
                return;
            if (!IsColumn(node.Args[0], Cc.MucLuong) || !IsField(node.Args[1], Fk.UnionDuesRate))
                return;

            node.Args[0] = SeedCappedSocialInsuranceBase();
        });

        column.FormulaJson = PayrollJson.Serialize(root);
    }

    private static FormulaNode SeedCappedSocialInsuranceBase()
    {
        var registeredBase = N.Add(
            N.Col(Cc.MucLuong),
            N.Field(Fk.ContractAllowanceNenPhuCapLuong));

        return N.If(
            N.Cmp(">", N.Field(Fk.ContractMucLuongCapBhxh), N.Const(0m)),
            N.Field(Fk.ContractMucLuongCapBhxh),
            registeredBase);
    }

    /// <summary>
    /// Bộ demo luôn seed đủ phân ca của tháng nên số ngày làm việc không hưởng lương được suy ra ngay
    /// trong công thức: công chuẩn trừ số ngày hưởng lương. Nhờ vậy cặp biên nghỉ 13/14 ngày được
    /// kiểm đúng mà không thay đổi collector runtime.
    /// </summary>
    private static FormulaNode SeedInsuranceMonthExemption() =>
        N.If(
            N.Cmp(">=",
                N.Sub(N.Col(Cc.CongChuan), N.Col(Cc.SoNgayCongHl)),
                N.Const(14m)),
            N.Const(1m),
            N.Const(0m));

    /// <summary>
    /// Chặn đúng lỗi cấu hình trong ảnh: OT, phụ trội ngày nghỉ/lễ, phụ cấp đêm và tổng thu nhập
    /// không được trở thành căn cứ BH/KPCĐ. Đồng thời công thức tháng không được chia lại theo công.
    /// </summary>
    private static void ValidateSeedInsuranceFormulas(IReadOnlyCollection<SalaryColumnDefinition> columns)
    {
        var insuranceCodes = new[]
        {
            Cc.DnBhxh, Cc.NldBhxh, Cc.DnBhyt, Cc.NldBhyt,
            Cc.DnBhtn, Cc.NldBhtn, Cc.Kpcd,
        };
        var forbiddenIncomeColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Cc.TangCa,
            Cc.PhuTroiNgayNghi,
            Cc.PhuCapDem,
            Cc.TongThuNhap,
            Cc.ThuNhap,
            Cc.LuongNgayPhep,
        };

        foreach (var code in insuranceCodes)
        {
            var column = RequireColumn(columns, code);
            var root = PayrollJson.Deserialize<FormulaNode>(column.FormulaJson!)
                ?? throw new InvalidOperationException($"Không đọc được công thức seed của cột {code} để kiểm tra.");

            var invalidColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hasAttendanceDivision = false;
            Visit(root, node =>
            {
                if (node.Type == FormulaNodeType.Column
                    && node.Text is { } referenced
                    && forbiddenIncomeColumns.Contains(referenced))
                    invalidColumns.Add(referenced);

                if (node.Type == FormulaNodeType.Binary && node.Text == "/" && node.Args.Count == 2
                    && IsColumn(node.Args[1], Cc.CongChuan))
                    hasAttendanceDivision = true;
            });

            if (invalidColumns.Count > 0)
                throw new InvalidOperationException(
                    $"Công thức seed {code} đang đưa khoản không thuộc căn cứ BH vào công thức: {string.Join(", ", invalidColumns)}.");
            if (hasAttendanceDivision)
                throw new InvalidOperationException($"Công thức seed {code} vẫn biến động theo công/công chuẩn.");
        }
    }

    private static FormulaNode UnwrapAttendanceProration(FormulaNode node)
    {
        // Mẫu cũ: (căn_cứ / CONGCHUAN) × công_có_BH. Chỉ gỡ đúng mẫu này, không đụng IF/cờ miễn.
        if (node.Type == FormulaNodeType.Binary && node.Text == "*" && node.Args.Count == 2)
        {
            var division = node.Args[0];
            if (division.Type == FormulaNodeType.Binary && division.Text == "/" && division.Args.Count == 2
                && IsColumn(division.Args[1], Cc.CongChuan))
                return division.Args[0];
        }

        throw new InvalidOperationException("Cấu trúc công thức BH mặc định đã đổi; không thể áp override seed an toàn.");
    }

    private static void SetFormula(
        IReadOnlyCollection<SalaryColumnDefinition> columns,
        string code,
        FormulaNode formula) =>
        RequireColumn(columns, code).FormulaJson = PayrollJson.Serialize(formula);

    private static SalaryColumnDefinition RequireColumn(
        IReadOnlyCollection<SalaryColumnDefinition> columns,
        string code) =>
        columns.SingleOrDefault(c => string.Equals(c.ColumnCode, code, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Thiếu cột hệ thống {code} trong bảng công thức seed.");

    private static bool IsColumn(FormulaNode node, string code) =>
        node.Type == FormulaNodeType.Column
        && string.Equals(node.Text, code, StringComparison.OrdinalIgnoreCase);

    private static bool IsField(FormulaNode node, string key) =>
        node.Type == FormulaNodeType.Field
        && string.Equals(node.Text, key, StringComparison.OrdinalIgnoreCase);

    private static void ReplaceNode(FormulaNode target, FormulaNode replacement)
    {
        target.Type = replacement.Type;
        target.Number = replacement.Number;
        target.Text = replacement.Text;
        target.Args = replacement.Args;
    }

    private static void Visit(FormulaNode node, Action<FormulaNode> visitor)
    {
        visitor(node);
        foreach (var child in node.Args) Visit(child, visitor);
    }
}
