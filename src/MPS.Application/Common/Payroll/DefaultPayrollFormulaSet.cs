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

        // [D20] — NLĐ làm không đủ công chuẩn (vào/nghỉ giữa tháng...) thì các khoản gắn với "nguyên
        // tháng" phải chia theo công thực tế/công chuẩn. Dùng CHUNG 1 công thức cho Cơm (GD) và cả
        // khối BH (GF): value / CongChuan × CongTinhCom (CongTinhCom = tổng công thực tế mọi loại
        // hình — chính thức + thử việc + học việc, xem PayrollDataCollector.congCom).
        FormulaNode ProrateByCong(FormulaNode value) =>
            N.Mul(N.Div(value, N.Col(Cc.CongChuan)), N.Col(Cc.CongTinhCom));

        FormulaNode Bh(string rateField, string capField) =>
            N.Func(Fn.InsuranceWithCap, ProrateByCong(N.Col(Cc.MucLuong)), N.Field(rateField), N.Field(capField));

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
        // #62/[D23] — Cờ BH là cột Input MỞ cho kế toán đè tay (thai sản "TS", ốm dài ngày); default
        // vẫn suy từ hợp đồng. (Cờ thuế FLAGTHUE ở Khối G giữ editable:false — khoán 20% là điểm chết.)
        Add(Cc.FlagBh, "Cờ BH", GA, ColumnDataType.Text, ColumnValueSource.Input, Fk.ContractInsuranceFlag, editable: true);

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
        // Cơm = tiền "Phụ cấp ăn ca" (MEAL) ghi trong HĐ của NV, CHIA theo công thực tế/công chuẩn
        // [D20] — làm nửa tháng thì hưởng nửa suất cơm, không phải nguyên suất.
        // PRORATE-02: nếu loại phụ cấp cơm được khai NGOẠI LỆ thì phần đó cộng NGUYÊN (không chia) —
        // vẫn đổ về chính cột này để suất cơm miễn thuế `MIN(COM, ngưỡng)` tính trên tổng.
        // Không có phụ cấp MEAL trong HĐ → 0. Cờ BH "xx" = không tính cơm (đặc tả §3-Khối D).
        Add(Cc.Com, "Cơm", GD, ColumnDataType.Money, ColumnValueSource.Formula,
            formula: N.If(N.Cmp("=", N.Col(Cc.FlagBh), N.Literal(InsuranceFlags.NoMealFlatTax)),
                N.Const(0m),
                N.Add(ProrateByCong(N.Field(Fk.ContractAllowanceCom)),
                    N.Field(Fk.ContractAllowanceComNgoaiLe))), money: true);
        Add(Cc.CongTac, "Hỗ trợ công tác", GD, ColumnDataType.Money, ColumnValueSource.Input, money: true);
        // #64/[D15] — cột "Khác" nhận mặc định TỔNG phụ cấp HĐ mã lạ (ngoài 4 mã cứng) để tiền không
        // biến mất; vẫn là Input nên kế toán chỉnh tay được (thưởng cưới/sinh...) đè lên mặc định.
        Add(Cc.Khac, "Khác (phụ cấp mã lạ / cưới, sinh...)", GD, ColumnDataType.Money, ColumnValueSource.Input, Fk.ContractAllowanceKhac, money: true);

        // ===== Khối E — thu nhập chịu thuế =====
        // PRORATE-02 — quy tắc chuẩn hoá: MỌI phụ cấp chia theo ngày công, TRỪ khoản khai ngoại lệ.
        //  • Cột "Khác" (`KHAC`, phụ cấp mã tự do — #64/[D15]) nay nằm TRONG nền chia theo công; trước
        //    đây nó được cộng nguyên ở nhóm `phuCapNgoai`, tức mã tự do vô tình đứng ngoài `[D20]`.
        //  • `ContractAllowanceNgoaiLe` (tổng khoản khai ngoại lệ, trừ cơm) bị TRỪ khỏi nền rồi CỘNG
        //    NGUYÊN vào `phuCapNgoai` ⇒ trả đủ bất kể ngày công, mà cột gốc vẫn hiện số hợp đồng.
        //  • `CONGTAC` (hỗ trợ công tác) giữ nguyên ở `phuCapNgoai`: đây là khoản chi theo chuyến, không
        //    do `AllowanceType` cấu hình nên không thuộc phạm vi cờ ngoại lệ.
        Add(Cc.ThuNhap, "Thu nhập", GE, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.Func(Fn.IncomeProration,
                N.Sub(
                    N.Func(Fn.Sum, N.Col(Cc.MucLuong), N.Col(Cc.PhuCapLuong), N.Col(Cc.TroCapThuViec),
                        N.Col(Cc.HocViec), N.Col(Cc.ThueNha), N.Col(Cc.DienThoai), N.Col(Cc.XangXe),
                        N.Col(Cc.Khac)),
                    N.Field(Fk.ContractAllowanceNgoaiLe)),
                N.Col(Cc.PhuCapLuong), N.Col(Cc.TroCapThuViec), N.Col(Cc.HocViec),
                N.Col(Cc.CongChuan), N.Col(Cc.CongTinhLuong), N.Col(Cc.CongThuViec), N.Col(Cc.CongHocViec),
                N.Func(Fn.Sum, N.Col(Cc.Com), N.Col(Cc.CongTac), N.Field(Fk.ContractAllowanceNgoaiLe))));
        Add(Cc.LuongNgayPhep, "Lương ngày phép", GE, ColumnDataType.Money, ColumnValueSource.Input, money: true);
        Add(Cc.TangCa, "Tăng ca", GE, ColumnDataType.Money, ColumnValueSource.Lookup, Fk.OtTangCa, money: true);
        Add(Cc.HoaHong, "Hoa hồng", GE, ColumnDataType.Money, ColumnValueSource.Input, money: true);
        Add(Cc.ThuongDuAn, "Thưởng dự án", GE, ColumnDataType.Money, ColumnValueSource.Lookup, Fk.BonusThuongDuAn, money: true);
        Add(Cc.ThuongNam, "Thưởng năm", GE, ColumnDataType.Money, ColumnValueSource.Input, money: true);
        Add(Cc.ThuongKhac, "Thưởng khác", GE, ColumnDataType.Money, ColumnValueSource.Input, money: true);
        // BS-2 — ca CHÍNH THỨC rơi vào ngày nghỉ tuần/lễ chỉ được trả 100% qua cột Thu nhập (phân bổ
        // theo ngày công); 2 cột này trả phần CÒN THIẾU tới 200%/300% (Đ.98.1) + phụ cấp đêm 30%
        // (Đ.98.2). Collector tính sẵn thành tiền, giống cách cột Tăng ca đang làm.
        Add(Cc.PhuTroiNgayNghi, "Phụ trội ngày nghỉ/lễ", GE, ColumnDataType.Money, ColumnValueSource.Lookup,
            Fk.PremiumDayType, money: true);
        Add(Cc.PhuCapDem, "Phụ cấp làm đêm", GE, ColumnDataType.Money, ColumnValueSource.Lookup,
            Fk.PremiumNight, money: true);
        Add(Cc.TongThuNhap, "Tổng thu nhập", GE, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.Func(Fn.Sum, N.Col(Cc.ThuNhap), N.Col(Cc.LuongNgayPhep), N.Col(Cc.TangCa),
                N.Col(Cc.HoaHong), N.Col(Cc.ThuongDuAn), N.Col(Cc.ThuongNam), N.Col(Cc.ThuongKhac),
                N.Col(Cc.PhuTroiNgayNghi), N.Col(Cc.PhuCapDem)));

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
                N.Func(Fn.InsuranceWithCap, ProrateByCong(N.Col(Cc.MucLuong)), N.Field(Fk.KpcdRate), N.Const(0m))));
        Add(Cc.CongBhDn, "Cộng BH – DN", GF, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.Func(Fn.Sum, N.Col(Cc.DnBhxh), N.Col(Cc.DnBhyt), N.Col(Cc.DnBhtn)));
        Add(Cc.CongBhNld, "Cộng BH – NLĐ", GF, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.Func(Fn.Sum, N.Col(Cc.NldBhxh), N.Col(Cc.NldBhyt), N.Col(Cc.NldBhtn)));

        // ===== Khối G — thuế TNCN =====
        Add(Cc.TruTamUng, "Cấn trừ tạm ứng", GG, ColumnDataType.Money, ColumnValueSource.Input, money: true);
        Add(Cc.SlNpt, "Số người phụ thuộc", GG, ColumnDataType.Number, ColumnValueSource.Lookup, Fk.DependentsCount);
        Add(Cc.FlagThue, "Cờ thuế", GG, ColumnDataType.Text, ColumnValueSource.Input, Fk.ContractTaxFlag, editable: false);
        Add(Cc.GtNpt, "Giảm trừ NPT", GG, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.If(N.Cmp("=", N.Col(Cc.FlagThue), N.Literal(TaxFlags.Progressive)),
                N.Mul(N.Col(Cc.SlNpt), N.Field(Fk.DependentDeduction)), N.Const(0m)));
        Add(Cc.GtBanThan, "Giảm trừ bản thân", GG, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.If(N.Cmp("=", N.Col(Cc.FlagThue), N.Literal(TaxFlags.Progressive)),
                N.Field(Fk.PersonalDeduction), N.Const(0m)));
        Add(Cc.ChenhLechLtg, "Chênh lệch làm thêm giờ", GG, ColumnDataType.Money, ColumnValueSource.Lookup, Fk.OtChenhLech, money: true);
        Add(Cc.ComMienThue, "Cơm miễn thuế", GG, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.Func(Fn.Min, N.Col(Cc.Com), N.Field(Fk.MealExemptThreshold)));
        // BS-2 — phụ trội ngày nghỉ/lễ + phụ cấp đêm là phần trả CAO HƠN mức ngày thường do làm đêm/
        // làm thêm ⇒ MIỄN thuế TNCN (Đ.3 TT111/2013), trừ khỏi TNCT giống "Chênh lệch làm thêm giờ".
        Add(Cc.Tnct, "Thu nhập chịu thuế", GG, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.Sub(N.Sub(N.Sub(N.Sub(N.Sub(
                N.Col(Cc.TongThuNhap), N.Col(Cc.DienThoai)), N.Col(Cc.ChenhLechLtg)), N.Col(Cc.ComMienThue)),
                N.Col(Cc.PhuTroiNgayNghi)), N.Col(Cc.PhuCapDem)));
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
        // BS-5 — 2 cột truy lĩnh / truy thu, seed từ field do handler tạo kỳ gom sẵn (mặc định 0 khi
        // NV không có khoản chưa áp). Dấu: TRUY_LINH dương (+); TRUY_THU âm (−) — cộng đại số vào THUCNHAN.
        Add(Cc.TruyLinh, "Truy lĩnh (kỳ trước sửa nguồn)", GH, ColumnDataType.Money, ColumnValueSource.Lookup,
            Fk.TruyLinh, money: true);
        Add(Cc.TruyThu, "Truy thu (kỳ trước sửa nguồn)", GH, ColumnDataType.Money, ColumnValueSource.Lookup,
            Fk.TruyThu, money: true);
        Add(Cc.ThucNhan, "THỰC NHẬN", GH, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            rounding: RoundingMode.ToThousand,
            formula: N.Add(N.Add(N.Sub(N.Sub(N.Add(N.Add(N.Sub(N.Sub(N.Sub(
                N.Col(Cc.TongThuNhap), N.Col(Cc.CongBhNld)), N.Col(Cc.TruTamUng)), N.Col(Cc.ThueTncn)),
                N.Col(Cc.ThuBhHoanThue)), N.Col(Cc.ThuKhac)), N.Col(Cc.PhiCongDoanVien)), N.Col(Cc.ChiKhac)),
                N.Col(Cc.TruyLinh)), N.Col(Cc.TruyThu)));

        return cols;
    }
}
