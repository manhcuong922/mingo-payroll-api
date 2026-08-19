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
            BhWithCap(rateField, N.Field(capField));

        FormulaNode BhWithCap(string rateField, FormulaNode capNode) =>
            N.Func(Fn.InsuranceWithCap, ProrateByCong(N.Col(Cc.MucLuong)), N.Field(rateField), capNode);

        // ⭐ D3 — BHYT có trần RIÊNG (hệ số trần BHYT trên màn hình cấu hình BHXH), trước đây bị bỏ
        // qua và dùng chung trần BHXH nên admin sửa ô đó không có tác dụng gì.
        // Fallback về trần BHXH khi trường mới = 0: snapshot input của kỳ tạo TRƯỚC bản sửa không có
        // khoá này, mà `InsuranceWithCap` coi cap = 0 là "KHÔNG áp trần" ⇒ không fallback thì kỳ cũ
        // sẽ trả THỪA cho người lương cao.
        var bhytCap = N.If(
            N.Cmp(">", N.Field(Fk.BhytCap), N.Const(0m)),
            N.Field(Fk.BhytCap),
            N.Field(Fk.BhxhBhytCap));

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
        // Phase 2 — tổng giờ làm (ca chính + tăng ca thực tế): căn cứ xếp bậc "Hỗ trợ theo tiến độ"
        // của HĐ khoán. Để kế toán đối chiếu vì sao ra mốc đó; không tham gia công thức nào khác.
        Add(Cc.TongGioLam, "Tổng giờ làm", GB, ColumnDataType.Number, ColumnValueSource.Lookup, Fk.TotalWorkHours);

        // ===== Khối C — lương & phụ cấp tính lương T13 =====
        Add(Cc.MucLuong, "Mức lương", GC, ColumnDataType.Money, ColumnValueSource.Input, Fk.ContractMucLuong, money: true);
        // Phase 2 — HĐ KHOÁN VIỆC không hưởng khoản theo hệ số năng suất; thay bằng "Hỗ trợ theo
        // tiến độ" ở khối E (cột riêng để trả NGUYÊN, không bị chia theo ngày công).
        // Cờ `contract.isPieceWork` mang ngữ nghĩa "CÓ phải khoán không" nên kỳ lương cũ (thiếu khoá
        // ⇒ ToFields điền 0) chạy đúng nhánh cũ.
        Add(Cc.PhuCapLuong, "Trợ cấp lương chính", GC, ColumnDataType.Money, ColumnValueSource.Formula,
            formula: N.If(
                N.Cmp("=", N.Field(Fk.ContractIsPieceWork), N.Const(1m)),
                N.Const(0m),
                N.Mul(N.Col(Cc.MucLuong), N.Field(Fk.ProductivityCoefficient))), money: true);
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
        // Phase 2 — HĐ khoán việc: khoản hỗ trợ theo mốc tổng giờ làm (từ 150h / từ 200h), collector
        // tra sẵn thành tiền từ bảng bậc `PieceWorkSupportBand`. Cộng THẲNG vào tổng thu nhập nên
        // KHÔNG bị chia theo ngày công — đủ giờ là nhận đủ, đúng thoả thuận trên hợp đồng.
        // Loại HĐ khác ⇒ field = 0 ⇒ cột này = 0.
        Add(Cc.HoTroTienDo, "Hỗ trợ theo tiến độ", GE, ColumnDataType.Money, ColumnValueSource.Lookup,
            Fk.PieceWorkSupport, money: true);
        Add(Cc.TongThuNhap, "Tổng thu nhập", GE, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.Func(Fn.Sum, N.Col(Cc.ThuNhap), N.Col(Cc.LuongNgayPhep), N.Col(Cc.TangCa),
                N.Col(Cc.HoaHong), N.Col(Cc.ThuongDuAn), N.Col(Cc.ThuongNam), N.Col(Cc.ThuongKhac),
                N.Col(Cc.PhuTroiNgayNghi), N.Col(Cc.PhuCapDem), N.Col(Cc.HoTroTienDo)));

        // ===== Khối F — bảo hiểm =====
        var noBh = new[] { InsuranceFlags.NoInsurance, InsuranceFlags.Maternity };
        var noBhOrHealthOnly = new[] { InsuranceFlags.NoInsurance, InsuranceFlags.Maternity, InsuranceFlags.HealthOnly };
        var noUnion = new[] { InsuranceFlags.NoUnion, InsuranceFlags.NoInsurance, InsuranceFlags.Maternity };

        // ⭐ A7 — mỗi cột BH nhân thêm CÔNG TẮC RIÊNG của hợp đồng (1/0).
        //
        // Cờ `FLAGBH` là enum thô chỉ biểu diễn được 3/8 tổ hợp bật/tắt của 3 loại bảo hiểm, nên
        // 5 tổ hợp còn lại bị gom nhầm vào "đóng đầy đủ" — điển hình: HĐ ghi *có BHXH, KHÔNG BHYT,
        // KHÔNG BHTN* vẫn bị trừ đủ BHYT lẫn BHTN, trái hợp đồng.
        //
        // Hai tầng tách bạch, cố ý giữ cả hai:
        //   • công tắc HĐ  — sự thật ghi trong hợp đồng, áp cho từng loại bảo hiểm độc lập;
        //   • cờ `FLAGBH`  — kênh ĐÈ TAY cho ngoại lệ theo kỳ (thai sản "TS", ốm dài ngày) [D23].
        // Cờ vẫn cắt được cả khối kể cả khi HĐ bật công tắc; ngược lại HĐ tắt công tắc nào thì
        // loại đó bằng 0 dù cờ là "đóng đầy đủ".
        //
        // Trường có ngữ nghĩa "MIỄN" (1 = miễn) chứ không phải "có đóng", để kỳ lương TẠO TRƯỚC bản
        // sửa này — snapshot input không có 3 khoá đó, `ToFields` điền 0 — vẫn ra đúng số cũ thay vì
        // bị xoá sạch bảo hiểm. Xem ghi chú ở `PayrollFieldKeys`.
        FormulaNode UnlessExempt(string exemptField, FormulaNode value) =>
            N.Mul(value, N.Sub(N.Const(1m), N.Field(exemptField)));

        Add(Cc.DnBhxh, "BHXH – DN", GF, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: SkipIfFlag(Cc.FlagBh, noBhOrHealthOnly,
                UnlessExempt(Fk.ContractNoSocialInsurance, Bh(Fk.BhxhDnRate, Fk.BhxhBhytCap))));
        Add(Cc.NldBhxh, "BHXH – NLĐ", GF, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: SkipIfFlag(Cc.FlagBh, noBhOrHealthOnly,
                UnlessExempt(Fk.ContractNoSocialInsurance, Bh(Fk.BhxhNldRate, Fk.BhxhBhytCap))));
        Add(Cc.DnBhyt, "BHYT – DN", GF, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: SkipIfFlag(Cc.FlagBh, noBh,
                UnlessExempt(Fk.ContractNoHealthInsurance, BhWithCap(Fk.BhytDnRate, bhytCap))));
        Add(Cc.NldBhyt, "BHYT – NLĐ", GF, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: SkipIfFlag(Cc.FlagBh, noBh,
                UnlessExempt(Fk.ContractNoHealthInsurance, BhWithCap(Fk.BhytNldRate, bhytCap))));
        Add(Cc.DnBhtn, "BHTN – DN", GF, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: SkipIfFlag(Cc.FlagBh, noBhOrHealthOnly,
                UnlessExempt(Fk.ContractNoUnemploymentInsurance, Bh(Fk.BhtnDnRate, Fk.BhtnCap))));
        Add(Cc.NldBhtn, "BHTN – NLĐ", GF, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: SkipIfFlag(Cc.FlagBh, noBhOrHealthOnly,
                UnlessExempt(Fk.ContractNoUnemploymentInsurance, Bh(Fk.BhtnNldRate, Fk.BhtnCap))));
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
        // Nhãn cố ý KHÔNG còn là "Chênh lệch làm thêm giờ": từ 01/07/2026 (Luật TNCN 109/2025 + NĐ
        // 253/2026) cột này mang TOÀN BỘ tiền tăng ca chứ không chỉ phần vượt 100%, xem
        // `LaborLawConstantsVersion.OvertimeIncomeFullyTaxExempt`. "Tăng ca miễn thuế" đúng cho cả hai
        // thời kỳ — luật cũ miễn phần chênh lệch, luật mới miễn toàn bộ.
        // Mã cột `CL_GLT`, `Cc.ChenhLechLtg` và lookup key giữ NGUYÊN: chúng là định danh đã đóng băng
        // trong snapshot của các kỳ lương cũ, đổi là hỏng phần tính lại kỳ cũ.
        Add(Cc.ChenhLechLtg, "Tăng ca miễn thuế", GG, ColumnDataType.Money, ColumnValueSource.Lookup, Fk.OtChenhLech, money: true);
        Add(Cc.ComMienThue, "Cơm miễn thuế", GG, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.Func(Fn.Min, N.Col(Cc.Com), N.Field(Fk.MealExemptThreshold)));
        // BS-2 — phụ trội ngày nghỉ/lễ + phụ cấp đêm là phần trả CAO HƠN mức ngày thường do làm đêm/
        // làm thêm ⇒ MIỄN thuế TNCN (Đ.3 TT111/2013), trừ khỏi TNCT giống cột "Tăng ca miễn thuế".
        Add(Cc.Tnct, "Thu nhập chịu thuế", GG, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.Sub(N.Sub(N.Sub(N.Sub(N.Sub(
                N.Col(Cc.TongThuNhap), N.Col(Cc.DienThoai)), N.Col(Cc.ChenhLechLtg)), N.Col(Cc.ComMienThue)),
                N.Col(Cc.PhuTroiNgayNghi)), N.Col(Cc.PhuCapDem)));
        Add(Cc.Tntt, "Thu nhập tính thuế", GG, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.Func(Fn.Max,
                N.Sub(N.Sub(N.Sub(N.Col(Cc.Tnct), N.Col(Cc.CongBhNld)), N.Col(Cc.GtNpt)), N.Col(Cc.GtBanThan)),
                N.Const(0m)));
        // Phase 7 — thuế KHOÁN của cá nhân cư trú không ký HĐLĐ / ký dưới 3 tháng (TT 111/2013
        // Đ.25.1.i): tỷ lệ + ngưỡng khấu trừ lấy từ Compliance thay vì hằng số, để đổi luật là sửa
        // trên màn hình PitRegulation chứ không phải deploy lại.
        //
        // • Tỷ lệ: kỳ lương cũ thiếu khoá ⇒ 0, mà 0 sẽ XOÁ SẠCH thuế khoán ⇒ fallback về 0.10 —
        //   đây là ngoại lệ có chủ đích của quy ước "thiếu khoá ⇒ 0 ⇒ giữ hành vi cũ".
        // • Ngưỡng: NĐ 253/2026 nâng 2.000.000 → 5.000.000 mỗi lần chi trả; bảng lương chi 1
        //   lần/tháng nên so với TỔNG THU NHẬP của kỳ. Dùng `<` chứ không `<=` vì điều khoản viết
        //   "từ 5.000.000 trở lên thì khấu trừ". Ngưỡng = 0 (kỳ cũ) ⇒ không dòng nào lọt ⇒ hành vi cũ.
        // • Ngưỡng KHÔNG áp cho nhánh 20%: mức đó dành cho cá nhân KHÔNG cư trú (Đ.25.1.a), không
        //   kèm ngưỡng miễn nào.
        var flatRate = N.If(N.Cmp(">", N.Field(Fk.ShortTermFlatRate), N.Const(0m)),
            N.Field(Fk.ShortTermFlatRate),
            N.Const(0.10m));
        var flatTax = N.If(
            N.Cmp("<", N.Col(Cc.TongThuNhap), N.Field(Fk.ShortTermTaxExemptIncome)),
            N.Const(0m),
            N.Mul(N.Col(Cc.TongThuNhap), flatRate));
        Add(Cc.ThueTncn, "Thuế TNCN", GG, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            rounding: RoundingMode.ToThreeDecimals,
            formula: N.If(N.Cmp("=", N.Col(Cc.FlagThue), N.Literal(TaxFlags.Khoan20)),
                N.Mul(N.Col(Cc.TongThuNhap), N.Const(0.20m)),
                N.If(N.Cmp("=", N.Col(Cc.FlagThue), N.Literal(TaxFlags.Khoan10)),
                    flatTax,
                    N.If(N.Cmp("=", N.Col(Cc.FlagThue), N.Literal(TaxFlags.CamKet)),
                        N.Const(0m),
                        N.Func(Fn.ProgressiveTax, N.Col(Cc.Tntt))))));

        // ===== Khối H — kết quả cuối =====
        Add(Cc.ThuBhHoanThue, "Thu do BH/hoàn thuế", GH, ColumnDataType.Money, ColumnValueSource.Input, money: true);
        Add(Cc.ThuKhac, "Thu khác", GH, ColumnDataType.Money, ColumnValueSource.Input, money: true);
        // A4 — ĐOÀN PHÍ công đoàn (QĐ 1908/QĐ-TLĐ Đ.23): 1% tiền lương làm căn cứ đóng BHXH, TRẦN
        // 10% mức lương cơ sở. Trước đây khai `Lookup` vào một field hardcode 0 ⇒ cột vừa không tự
        // tính vừa không cho kế toán nhập, chết cứng ở 0 với mọi dữ liệu đầu vào.
        //
        // Khác cả khối BH ở chỗ trần chặn trên SỐ TIỀN đóng chứ không trên nền đóng, nên KHÔNG dùng
        // được `BH_CO_TRAN` (hàm đó = MIN(nền, trần) × tỷ_lệ) — phải MIN sau khi đã nhân tỷ lệ.
        // Giữ quy ước "trần = 0 nghĩa là không áp trần" cho đồng nhất với `BH_CO_TRAN`, vì kỳ lương
        // tạo trước bản sửa này không có khoá trần (ToFields điền 0).
        //
        // Nhân cờ đoàn viên ở ngoài cùng: đoàn phí CHỈ thu của người có tham gia công đoàn, khác
        // KPCĐ là nghĩa vụ của DN với mọi NLĐ.
        //
        // KHÔNG `ProrateByCong`: đoàn phí là khoản đóng theo THÁNG trên tiền lương làm căn cứ đóng
        // BHXH, không chia theo ngày công thực tế — khác khối BH và tiền cơm. Đối chiếu file lương
        // mẫu 08/2023 (PayrollGoldenReconciliationTests): 14.641.000 × 2% = 292.820 đúng bằng cột
        // đoàn phí trên file, prorate vào sẽ lệch.
        var unionDuesRaw = N.Mul(N.Col(Cc.MucLuong), N.Field(Fk.UnionDuesRate));
        Add(Cc.PhiCongDoanVien, "Phí công đoàn viên", GH, ColumnDataType.Money, ColumnValueSource.Formula, money: true,
            formula: N.Mul(
                N.If(N.Cmp(">", N.Field(Fk.UnionDuesCap), N.Const(0m)),
                    N.Func(Fn.Min, unionDuesRaw, N.Field(Fk.UnionDuesCap)),
                    unionDuesRaw),
                N.Field(Fk.UnionMember)));
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
