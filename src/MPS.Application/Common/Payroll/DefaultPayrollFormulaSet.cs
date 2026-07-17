using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using N = MPS.Application.Common.Payroll.Formula.FormulaNode;
using Cc = MPS.Domain.Constants.PayrollColumnCodes;
using Fk = MPS.Application.Common.Payroll.PayrollFieldKeys;
using Fn = MPS.Domain.Constants.PayrollFunctions;
using FormulaNode = MPS.Application.Common.Payroll.Formula.FormulaNode;

namespace MPS.Application.Common.Payroll;

/// <summary>
/// Bộ cột + công thức MẶC ĐỊNH tái hiện đặc tả sheet "4. LUONG". Kế toán clone bảng này rồi chỉnh.
/// Trả về list SalaryColumnDefinition (FormulaSetId gán khi persist). Xem docs/DAC_TA_BANG_LUONG.md.
/// </summary>
public static class DefaultPayrollFormulaSet
{
    public const string Name = "Bảng lương chuẩn công ty";

    public static List<SalaryColumnDefinition> BuildColumns()
    {
        var cols = new List<SalaryColumnDefinition>();
        var order = 0;

        void Add(string code, string label, string group, ColumnDataType type, ColumnValueSource source,
            string? lookupKey = null, FormulaNode? formula = null, RoundingMode rounding = RoundingMode.None,
            bool money = false, bool editable = true)
        {
            cols.Add(new SalaryColumnDefinition
            {
                ColumnCode = code,
                Label = label,
                GroupName = group,
                DisplayOrder = order++,
                DataType = type,
                ValueSource = source,
                LookupKey = lookupKey,
                FormulaJson = formula is null ? null : PayrollJson.Serialize(formula),
                Rounding = rounding,
                IsSystem = true,
                IsMoney = money,
                IsEditable = editable,
            });
        }

        // Nested IF: bỏ qua (=0) nếu cờ BH thuộc tập, ngược lại tính otherwise.
        FormulaNode SkipIfFlag(string flagCol, string[] skipFlags, FormulaNode otherwise)
        {
            var node = otherwise;
            for (var i = skipFlags.Length - 1; i >= 0; i--)
                node = N.If(N.Cmp("=", N.Col(flagCol), N.Literal(skipFlags[i])), N.Const(0m), node);
            return node;
        }

        FormulaNode Bh(string rateField, string capField) =>
            N.Func(Fn.InsuranceWithCap, N.Col(Cc.MucLuong), N.Field(rateField), N.Field(capField));

        const string GA = "Nhân viên";
        const string GB = "Ngày công";
        const string GC = "Lương & phụ cấp (tính lương T13)";
        const string GD = "Phụ cấp (không tính lương T13)";
        const string GE = "Thu nhập chịu thuế TNCN";
        const string GF = "Bảo hiểm";
        const string GG = "Thuế TNCN";
        const string GH = "Kết quả cuối";

        // ===== Khối A — nhân viên =====
        Add(Cc.MaNv, "Mã NV", GA, ColumnDataType.Text, ColumnValueSource.Lookup, Fk.EmployeeCode, editable: false);
        Add(Cc.TenNv, "Họ tên", GA, ColumnDataType.Text, ColumnValueSource.Lookup, Fk.EmployeeFullName, editable: false);
        Add(Cc.ChucVu, "Chức vụ", GA, ColumnDataType.Text, ColumnValueSource.Lookup, Fk.EmployeePosition, editable: false);
        Add(Cc.SoTaiKhoan, "Số tài khoản", GA, ColumnDataType.Text, ColumnValueSource.Lookup, Fk.EmployeeBankAccount, editable: false);
        Add(Cc.NgayVao, "Ngày vào", GA, ColumnDataType.Text, ColumnValueSource.Lookup, Fk.EmployeeJoinDate, editable: false);
        Add(Cc.Thang, "Tháng", GA, ColumnDataType.Text, ColumnValueSource.Lookup, Fk.PeriodMonthLabel, editable: false);
        Add(Cc.FlagBh, "Cờ BH", GA, ColumnDataType.Text, ColumnValueSource.Input, Fk.ContractInsuranceFlag);

        // ===== Khối B — ngày công =====
        Add(Cc.CongChuan, "Công chuẩn", GB, ColumnDataType.Number, ColumnValueSource.Lookup, Fk.CongChuan);
        Add(Cc.CongTinhCom, "Công tính cơm", GB, ColumnDataType.Number, ColumnValueSource.Lookup, Fk.CongTinhCom);
        Add(Cc.CongThuViec, "Công thử việc", GB, ColumnDataType.Number, ColumnValueSource.Lookup, Fk.CongThuViec);
        Add(Cc.CongTinhLuong, "Công chính thức", GB, ColumnDataType.Number, ColumnValueSource.Lookup, Fk.CongChinhThuc);
        Add(Cc.CongHocViec, "Công học việc", GB, ColumnDataType.Number, ColumnValueSource.Lookup, Fk.CongHocViec);

        // ===== Khối C — lương & phụ cấp tính lương T13 =====
        Add(Cc.MucLuong, "Mức lương", GC, ColumnDataType.Money, ColumnValueSource.Input, Fk.ContractMucLuong, money: true);
        Add(Cc.PhuCapLuong, "Trợ cấp lương chính", GC, ColumnDataType.Money, ColumnValueSource.Formula,
            formula: N.Mul(N.Col(Cc.MucLuong), N.Field(Fk.ProductivityCoefficient)), money: true);
        Add(Cc.TroCapThuViec, "Trợ cấp thử việc", GC, ColumnDataType.Money, ColumnValueSource.Formula,
            formula: N.Col(Cc.PhuCapLuong), money: true);
        Add(Cc.HocViec, "Học việc", GC, ColumnDataType.Money, ColumnValueSource.Input, money: true);
        Add(Cc.ThueNha, "Thuê nhà", GC, ColumnDataType.Money, ColumnValueSource.Input, Fk.ContractAllowanceThueNha, money: true);
        Add(Cc.DienThoai, "Điện thoại", GC, ColumnDataType.Money, ColumnValueSource.Input, Fk.ContractAllowanceDienThoai, money: true);

        // ===== Khối D — phụ cấp không tính lương T13 =====
        Add(Cc.XangXe, "Xăng xe", GD, ColumnDataType.Money, ColumnValueSource.Input, Fk.ContractAllowanceXangXe, money: true);
        // Cơm = tiền "Phụ cấp ăn ca" (MEAL) ghi trong HĐ của NV — nguyên tháng, KHÔNG nhân số công.
        // Không có phụ cấp MEAL trong HĐ → 0. Cờ BH "xx" = không tính cơm (đặc tả §3-Khối D).
        Add(Cc.Com, "Cơm", GD, ColumnDataType.Money, ColumnValueSource.Formula,
            formula: N.If(N.Cmp("=", N.Col(Cc.FlagBh), N.Literal(InsuranceFlags.NoMealFlatTax)),
                N.Const(0m), N.Field(Fk.ContractAllowanceCom)), money: true);
        Add(Cc.CongTac, "Hỗ trợ công tác", GD, ColumnDataType.Money, ColumnValueSource.Input, money: true);
        Add(Cc.Khac, "Khác (cưới/sinh...)", GD, ColumnDataType.Money, ColumnValueSource.Input, money: true);

        // ===== Khối E — thu nhập chịu thuế =====
        Add(Cc.ThuNhap, "Thu nhập", GE, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.Func(Fn.IncomeProration,
                N.Func(Fn.Sum, N.Col(Cc.MucLuong), N.Col(Cc.PhuCapLuong), N.Col(Cc.TroCapThuViec),
                    N.Col(Cc.HocViec), N.Col(Cc.ThueNha), N.Col(Cc.DienThoai), N.Col(Cc.XangXe)),
                N.Col(Cc.PhuCapLuong), N.Col(Cc.TroCapThuViec), N.Col(Cc.HocViec),
                N.Col(Cc.CongChuan), N.Col(Cc.CongTinhLuong), N.Col(Cc.CongThuViec), N.Col(Cc.CongHocViec),
                N.Func(Fn.Sum, N.Col(Cc.Com), N.Col(Cc.CongTac), N.Col(Cc.Khac))));
        Add(Cc.LuongNgayPhep, "Lương ngày phép", GE, ColumnDataType.Money, ColumnValueSource.Input, money: true);
        Add(Cc.TangCa, "Tăng ca", GE, ColumnDataType.Money, ColumnValueSource.Lookup, Fk.OtTangCa, money: true);
        Add(Cc.HoaHong, "Hoa hồng", GE, ColumnDataType.Money, ColumnValueSource.Input, money: true);
        Add(Cc.ThuongDuAn, "Thưởng dự án", GE, ColumnDataType.Money, ColumnValueSource.Lookup, Fk.BonusThuongDuAn, money: true);
        Add(Cc.ThuongNam, "Thưởng năm", GE, ColumnDataType.Money, ColumnValueSource.Input, money: true);
        Add(Cc.ThuongKhac, "Thưởng khác", GE, ColumnDataType.Money, ColumnValueSource.Input, money: true);
        Add(Cc.TongThuNhap, "Tổng thu nhập", GE, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.Func(Fn.Sum, N.Col(Cc.ThuNhap), N.Col(Cc.LuongNgayPhep), N.Col(Cc.TangCa),
                N.Col(Cc.HoaHong), N.Col(Cc.ThuongDuAn), N.Col(Cc.ThuongNam), N.Col(Cc.ThuongKhac)));

        // ===== Khối F — bảo hiểm =====
        var noBh = new[] { InsuranceFlags.NoInsurance, InsuranceFlags.Maternity };
        var noBhOrHealthOnly = new[] { InsuranceFlags.NoInsurance, InsuranceFlags.Maternity, InsuranceFlags.HealthOnly };
        var noUnion = new[] { InsuranceFlags.NoUnion, InsuranceFlags.NoInsurance, InsuranceFlags.Maternity };

        Add(Cc.DnBhxh, "BHXH – DN", GF, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: SkipIfFlag(Cc.FlagBh, noBhOrHealthOnly, Bh(Fk.BhxhDnRate, Fk.BhxhBhytCap)));
        Add(Cc.NldBhxh, "BHXH – NLĐ", GF, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: SkipIfFlag(Cc.FlagBh, noBhOrHealthOnly, Bh(Fk.BhxhNldRate, Fk.BhxhBhytCap)));
        Add(Cc.DnBhyt, "BHYT – DN", GF, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: SkipIfFlag(Cc.FlagBh, noBh, Bh(Fk.BhytDnRate, Fk.BhxhBhytCap)));
        Add(Cc.NldBhyt, "BHYT – NLĐ", GF, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: SkipIfFlag(Cc.FlagBh, noBh, Bh(Fk.BhytNldRate, Fk.BhxhBhytCap)));
        Add(Cc.DnBhtn, "BHTN – DN", GF, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: SkipIfFlag(Cc.FlagBh, noBhOrHealthOnly, Bh(Fk.BhtnDnRate, Fk.BhtnCap)));
        Add(Cc.NldBhtn, "BHTN – NLĐ", GF, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: SkipIfFlag(Cc.FlagBh, noBhOrHealthOnly, Bh(Fk.BhtnNldRate, Fk.BhtnCap)));
        Add(Cc.Kpcd, "KPCĐ (DN)", GF, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: SkipIfFlag(Cc.FlagBh, noUnion,
                N.Func(Fn.InsuranceWithCap, N.Col(Cc.MucLuong), N.Field(Fk.KpcdRate), N.Const(0m))));
        Add(Cc.CongBhDn, "Cộng BH – DN", GF, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.Func(Fn.Sum, N.Col(Cc.DnBhxh), N.Col(Cc.DnBhyt), N.Col(Cc.DnBhtn)));
        Add(Cc.CongBhNld, "Cộng BH – NLĐ", GF, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.Func(Fn.Sum, N.Col(Cc.NldBhxh), N.Col(Cc.NldBhyt), N.Col(Cc.NldBhtn)));

        // ===== Khối G — thuế TNCN =====
        Add(Cc.TruTamUng, "Cấn trừ tạm ứng", GG, ColumnDataType.Money, ColumnValueSource.Input, money: true);
        Add(Cc.SlNpt, "Số người phụ thuộc", GG, ColumnDataType.Number, ColumnValueSource.Lookup, Fk.DependentsCount);
        Add(Cc.FlagThue, "Cờ thuế", GG, ColumnDataType.Text, ColumnValueSource.Input, Fk.ContractTaxFlag);
        Add(Cc.GtNpt, "Giảm trừ NPT", GG, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.If(N.Cmp("=", N.Col(Cc.FlagThue), N.Literal(TaxFlags.Progressive)),
                N.Mul(N.Col(Cc.SlNpt), N.Field(Fk.DependentDeduction)), N.Const(0m)));
        Add(Cc.GtBanThan, "Giảm trừ bản thân", GG, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.If(N.Cmp("=", N.Col(Cc.FlagThue), N.Literal(TaxFlags.Progressive)),
                N.Field(Fk.PersonalDeduction), N.Const(0m)));
        Add(Cc.ChenhLechLtg, "Chênh lệch làm thêm giờ", GG, ColumnDataType.Money, ColumnValueSource.Lookup, Fk.OtChenhLech, money: true);
        Add(Cc.ComMienThue, "Cơm miễn thuế", GG, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.Func(Fn.Min, N.Col(Cc.Com), N.Field(Fk.MealExemptThreshold)));
        Add(Cc.Tnct, "Thu nhập chịu thuế", GG, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.Sub(N.Sub(N.Sub(N.Col(Cc.TongThuNhap), N.Col(Cc.DienThoai)), N.Col(Cc.ChenhLechLtg)), N.Col(Cc.ComMienThue)));
        Add(Cc.Tntt, "Thu nhập tính thuế", GG, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.Func(Fn.Max,
                N.Sub(N.Sub(N.Sub(N.Col(Cc.Tnct), N.Col(Cc.CongBhNld)), N.Col(Cc.GtNpt)), N.Col(Cc.GtBanThan)),
                N.Const(0m)));
        Add(Cc.ThueTncn, "Thuế TNCN", GG, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            rounding: RoundingMode.ToThreeDecimals,
            formula: N.If(N.Cmp("=", N.Col(Cc.FlagThue), N.Literal(TaxFlags.Khoan20)),
                N.Mul(N.Col(Cc.TongThuNhap), N.Const(0.20m)),
                N.If(N.Cmp("=", N.Col(Cc.FlagThue), N.Literal(TaxFlags.Khoan10)),
                    N.Mul(N.Col(Cc.TongThuNhap), N.Const(0.10m)),
                    N.If(N.Cmp("=", N.Col(Cc.FlagThue), N.Literal(TaxFlags.CamKet)),
                        N.Const(0m),
                        N.Func(Fn.ProgressiveTax, N.Col(Cc.Tntt))))));

        // ===== Khối H — kết quả cuối =====
        Add(Cc.ThuBhHoanThue, "Thu do BH/hoàn thuế", GH, ColumnDataType.Money, ColumnValueSource.Input, money: true);
        Add(Cc.ThuKhac, "Thu khác", GH, ColumnDataType.Money, ColumnValueSource.Input, money: true);
        Add(Cc.PhiCongDoanVien, "Phí công đoàn viên", GH, ColumnDataType.Money, ColumnValueSource.Lookup, Fk.UnionFee, money: true);
        Add(Cc.ChiKhac, "Chi khác", GH, ColumnDataType.Money, ColumnValueSource.Input, money: true);
        Add(Cc.ThucNhan, "THỰC NHẬN", GH, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            rounding: RoundingMode.ToThousand,
            formula: N.Sub(N.Sub(N.Add(N.Add(N.Sub(N.Sub(N.Sub(
                N.Col(Cc.TongThuNhap), N.Col(Cc.CongBhNld)), N.Col(Cc.TruTamUng)), N.Col(Cc.ThueTncn)),
                N.Col(Cc.ThuBhHoanThue)), N.Col(Cc.ThuKhac)), N.Col(Cc.PhiCongDoanVien)), N.Col(Cc.ChiKhac)));

        return cols;
    }
}
