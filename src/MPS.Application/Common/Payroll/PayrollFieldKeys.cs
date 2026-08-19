namespace MPS.Application.Common.Payroll;

/// <summary>
/// Khoá field (whitelist) mà công thức/cột Lookup tham chiếu qua <c>field(key)</c>. Collector điền
/// giá trị theo các khoá này; catalog cấp nhãn cho explanation. Cột Input dùng key làm giá trị mặc định.
/// </summary>
public static class PayrollFieldKeys
{
    // ===== Định danh (text) =====
    public const string EmployeeCode = "employee.code";
    public const string EmployeeFullName = "employee.fullName";
    public const string EmployeePosition = "employee.position";
    public const string EmployeeBankAccount = "employee.bankAccount";
    public const string EmployeeJoinDate = "employee.joinDate";
    public const string PeriodMonthLabel = "period.monthLabel";

    // ===== Mặc định cho cột Input =====
    public const string ContractMucLuong = "contract.mucLuong";
    /// <summary>
    /// ⭐ Phase 5 — TRỢ CẤP THỬ VIỆC: phần cộng thêm để những ngày còn THỬ VIỆC trong kỳ được trả
    /// theo mức thử việc THẬT trên hợp đồng, trong khi <see cref="ContractMucLuong"/> mang mức
    /// CHÍNH THỨC.
    ///
    /// <para>Suy ra từ chính nhánh thử việc của <c>THU_NHAP_PHANBO</c>:
    /// <c>(MUCLUONG + TROCAPTHUVIEC + phụ_cấp)/J × công_thử_việc</c>, nên
    /// <c>troCapThuViec = MAX(0, mứcThửViệc × (1 + hệ_số_năng_suất) − mứcChínhThức)</c> cho ra đúng
    /// "trả theo mức thử việc ghi trên HĐ".</para>
    ///
    /// <para>⚠️ KHÔNG dùng hằng số <c>PHUCAPLUONG × 0.85</c>: với HĐ thử việc riêng thì
    /// <c>MUCLUONG</c> đã là <c>ProbationSalary</c> (85%) nên nhân tiếp 0.85 ra 72,25% — THẤP HƠN
    /// sàn Điều 26 BLLĐ 2019, tức tự tạo ra vi phạm. Ngoài ra 85% là mức SÀN, hợp đồng có thể ghi
    /// 90%/95%.</para>
    ///
    /// <para>Kỳ không có ngày thử việc ⇒ collector điền 0; kỳ lương cũ dùng công thức đã đóng băng
    /// (<c>TROCAPTHUVIEC = PHUCAPLUONG</c>) nên không đọc khoá này ⇒ số không đổi.</para>
    /// </summary>
    public const string ContractTroCapThuViec = "contract.troCapThuViec";

    public const string ContractInsuranceFlag = "contract.insuranceFlag"; // text
    public const string ContractTaxFlag = "contract.taxFlag";             // text

    // ⭐ A7 — ba công tắc bảo hiểm ĐỘC LẬP của hợp đồng.
    //
    // Cờ `ContractInsuranceFlag` là enum thô chỉ biểu diễn được 3/8 tổ hợp bật/tắt, nên các tổ hợp
    // còn lại (vd có BHXH nhưng KHÔNG BHYT) bị gom nhầm vào "đóng đầy đủ" ⇒ vẫn trừ BHYT trái hợp
    // đồng. Ba trường này để mỗi cột BH tôn trọng đúng công tắc của chính nó; cờ vẫn giữ vai trò
    // ĐÈ TAY cho ngoại lệ theo kỳ (thai sản, ốm dài ngày) theo [D23].
    //
    // ⚠️ Ngữ nghĩa cố ý là "KHÔNG đóng" (1 = miễn, 0 = đóng), KHÔNG phải "có đóng".
    // Lý do: `PayrollEmployeeInputs.ToFields` điền 0 cho mọi khoá thiếu, mà snapshot input của các
    // kỳ lương TẠO TRƯỚC bản sửa này không có 3 khoá đó. Nếu dùng ngữ nghĩa "có đóng" thì thiếu
    // khoá ⇒ 0 ⇒ hệ số nhân 0 ⇒ **xoá sạch bảo hiểm** khi kế toán sửa một ô trên kỳ cũ.
    // Với ngữ nghĩa "miễn", thiếu khoá ⇒ 0 ⇒ không miễn ⇒ giữ nguyên số cũ.
    public const string ContractNoSocialInsurance = "contract.noSocialInsurance";
    public const string ContractNoHealthInsurance = "contract.noHealthInsurance";
    public const string ContractNoUnemploymentInsurance = "contract.noUnemploymentInsurance";
    public const string ContractAllowanceThueNha = "contract.allowanceThueNha";
    public const string ContractAllowanceDienThoai = "contract.allowanceDienThoai";
    public const string ContractAllowanceXangXe = "contract.allowanceXangXe";
    public const string ContractAllowanceCom = "contract.allowanceCom";
    // #64/[D15] — tổng các phụ cấp HĐ có mã KHÁC 4 mã cứng (HOUSING/PHONE/FUEL/MEAL) để không mất tiền.
    public const string ContractAllowanceKhac = "contract.allowanceKhac";

    // ===== PRORATE-02 — phụ cấp khai NGOẠI LỆ (`AllowanceType.ExcludeFromProration`) =====
    /// <summary>
    /// Tổng phụ cấp ngoại lệ (trừ cơm): bị TRỪ khỏi nền chia theo công rồi CỘNG NGUYÊN trở lại,
    /// nên cột gốc vẫn hiện đủ số hợp đồng mà tiền không bị chia theo ngày công.
    /// </summary>
    public const string ContractAllowanceNgoaiLe = "contract.allowanceNgoaiLe";
    /// <summary>Phần tiền cơm khai ngoại lệ — cộng thẳng vào cột COM, không qua bước chia theo công.</summary>
    public const string ContractAllowanceComNgoaiLe = "contract.allowanceComNgoaiLe";

    // ===== Chấm công =====
    public const string CongChuan = "period.congChuan";
    public const string CongTinhCom = "attendance.congTinhCom";
    public const string CongThuViec = "attendance.congThuViec";
    public const string CongChinhThuc = "attendance.congChinhThuc";
    public const string CongHocViec = "attendance.congHocViec";

    /// <summary>
    /// ⭐ Phase 5 — số công của những ngày mà hợp đồng phủ ngày đó KHÔNG đóng bảo hiểm nào (cả 3 cờ
    /// BHXH/BHYT/BHTN đều tắt, điển hình là HĐ thử việc riêng). Nền đóng BH của kỳ =
    /// <c>CONGTINHCOM − khoá này</c>, để nửa tháng thử việc không bị tính BH.
    ///
    /// <para>⚠️ Cố ý mang ngữ nghĩa NGƯỢC ("công KHÔNG có BH") thay vì "công có BH": kỳ lương cũ
    /// thiếu khoá ⇒ <c>ToFields</c> điền 0 ⇒ trừ 0 ⇒ nền đóng vẫn là toàn bộ công, đúng hành vi cũ.
    /// Nếu để chiều thuận thì 0 vừa là "kỳ cũ" vừa là "không ngày nào có BH", không phân biệt được
    /// ⇒ phải fallback và sẽ thu BH nhầm cho người có 0 ngày được đóng.</para>
    /// </summary>
    public const string CongKhongBaoHiem = "attendance.congKhongBaoHiem";

    /// <summary>
    /// ⭐ CC-2026-08 — số công NGÀY LỄ được cộng tự động (Đ.112 BLLĐ: nghỉ lễ hưởng nguyên lương).
    /// Chỉ để kế toán ĐỐI CHIẾU vì sao tổng công khớp công chuẩn dù NLĐ nghỉ lễ; KHÔNG cột công
    /// thức nào chia cho khoá này.
    /// </summary>
    public const string CongLe = "attendance.congLe";

    /// <summary>
    /// ⭐ CC-2026-08 — TỔNG công HƯỞNG LƯƠNG của kỳ = công đi làm thật + công ngày lễ được cộng
    /// (= <c>CONGCHINHTHUC + CONGTHUVIEC + CONGHOCVIEC</c>). Là mẫu số đúng cho nền đóng BH.
    ///
    /// <para>⚠️ KHÁC <see cref="CongTinhCom"/>: cơm chỉ tính ngày ĐI LÀM THẬT (nghỉ lễ ở nhà không
    /// ăn cơm ca) nên `CONGTINHCOM` cố ý KHÔNG gồm công lễ. Trước CC-2026-08 hai khái niệm này
    /// trùng nhau nên dùng chung một khoá; nay phải tách, nếu không nền đóng BHXH sẽ tụt đúng bằng
    /// tỉ lệ ngày lễ (tháng Tết ~23%) vì mẫu số công chuẩn to lên mà tử số giữ nguyên.</para>
    ///
    /// <para>Kỳ lương cũ thiếu khoá ⇒ <c>ToFields</c> điền 0 ⇒ công thức tự lùi về `CONGTINHCOM`,
    /// giữ nguyên số đã chốt. Xem `DefaultPayrollFormulaSet.ProrateByCongCoBaoHiem`.</para>
    /// </summary>
    public const string CongHuongLuong = "attendance.congHuongLuong";

    public const string DayHours = "attendance.dayHours";
    public const string NightHours = "attendance.nightHours";
    public const string AbsentDays = "attendance.absentDays";
    public const string LateDays = "attendance.lateDays";

    /// <summary>
    /// ⭐ Phase 2 — TỔNG giờ làm trong kỳ = giờ ca chính (ngày + đêm) + giờ tăng ca THỰC TẾ đã chấm
    /// công. Là căn cứ xếp bậc hỗ trợ theo tiến độ của HĐ khoán việc.
    ///
    /// <para>Kỳ lương tạo TRƯỚC bản sửa này thiếu khoá ⇒ <c>ToFields</c> điền 0 ⇒ không đạt mốc nào
    /// ⇒ tiền hỗ trợ = 0, đúng bằng hành vi cũ.</para>
    /// </summary>
    public const string TotalWorkHours = "attendance.totalWorkHours";

    // ===== Tăng ca =====
    public const string OtTangCa = "ot.tangCa";
    public const string OtChenhLech = "ot.chenhLechLtg";

    // ===== Phụ trội giờ THƯỜNG (BS-2 phần A) — ca chính thức, không qua đơn OT =====
    /// <summary>Tiền phụ trội do ca rơi vào ngày nghỉ tuần/lễ (phần vượt hệ số ngày thường).</summary>
    public const string PremiumDayType = "premium.ngayNghiLe";
    /// <summary>Tiền phụ cấp làm đêm 30% (Đ.98.2) cho giờ thường.</summary>
    public const string PremiumNight = "premium.lamDem";
    /// <summary>Số giờ công thường rơi vào ngày nghỉ tuần (để HR đối chiếu).</summary>
    public const string WeeklyRestHours = "attendance.weeklyRestHours";
    /// <summary>Số giờ công thường rơi vào ngày lễ / nghỉ có lương (để HR đối chiếu).</summary>
    public const string HolidayHours = "attendance.holidayHours";

    // ===== Đánh giá / năng suất =====
    public const string EvalFinalScore = "eval.finalScore";
    public const string ProductivityCoefficient = "eval.productivityCoefficient";

    // ===== Truy lĩnh / truy thu (BS-5) — gom từ các kỳ Finalized đã sửa nguồn =====
    public const string TruyLinh = "adjustment.truyLinh";   // tổng phần dương (+)
    public const string TruyThu = "adjustment.truyThu";     // tổng phần âm (−, đã clamp để THỰC NHẬN ≥ 0)

    // ===== HĐ khoán việc (Phase 2) =====
    /// <summary>
    /// 1 = hợp đồng đang hiệu lực là KHOÁN VIỆC. Công thức dùng cờ này để đổi nhánh cột "Trợ cấp
    /// lương chính".
    ///
    /// <para>⚠️ Ngữ nghĩa cố ý là "CÓ phải khoán không" (1 = khoán): kỳ lương cũ thiếu khoá ⇒
    /// <c>ToFields</c> điền 0 ⇒ không phải khoán ⇒ công thức chạy đúng nhánh cũ. Ngược lại (0 =
    /// khoán) sẽ biến mọi dòng lương của kỳ cũ thành HĐ khoán khi tính lại.</para>
    /// </summary>
    public const string ContractIsPieceWork = "contract.isPieceWork";

    /// <summary>
    /// Tiền HỖ TRỢ THEO TIẾN ĐỘ của HĐ khoán, collector tra sẵn từ bảng bậc
    /// <c>PieceWorkSupportBand</c> theo <see cref="TotalWorkHours"/>. Loại HĐ khác ⇒ 0.
    ///
    /// <para>Tên hiển thị trên bảng lương/phiếu lương/Excel là "Hỗ trợ theo tiến độ" — KHÔNG dùng
    /// chữ "lương"/"thưởng" cho khoản của hợp đồng dân sự.</para>
    /// </summary>
    public const string PieceWorkSupport = "support.hoTroTienDo";

    // ===== Thưởng / công đoàn / người phụ thuộc =====
    public const string BonusThuongDuAn = "bonus.thuongDuAn";
    public const string DependentsCount = "employee.dependentsCount";

    // NLĐ có tham gia công đoàn (1 = đoàn viên) — chỉ đoàn viên mới bị thu ĐOÀN PHÍ.
    // Ngữ nghĩa thuận (1 = có) an toàn ở đây, khác 3 khoá `Contract*NoInsurance` phải đảo:
    // kỳ lương tạo TRƯỚC bản sửa này thiếu khoá ⇒ `ToFields` điền 0 ⇒ không phải đoàn viên
    // ⇒ đoàn phí = 0, đúng bằng hành vi cũ (cột này vốn hardcode 0). Không hồi tố thu tiền của ai.
    public const string UnionMember = "employee.isUnionMember";

    // ===== Compliance — tỷ lệ (phân số) =====
    public const string BhxhDnRate = "compliance.bhxhDnRate";
    public const string BhxhNldRate = "compliance.bhxhNldRate";
    public const string BhytDnRate = "compliance.bhytDnRate";
    public const string BhytNldRate = "compliance.bhytNldRate";
    public const string BhtnDnRate = "compliance.bhtnDnRate";
    public const string BhtnNldRate = "compliance.bhtnNldRate";
    public const string KpcdRate = "compliance.kpcdRate";

    /// <summary>Tỷ lệ ĐOÀN PHÍ công đoàn NLĐ đóng (QĐ 1908/QĐ-TLĐ: 1% lương đóng BHXH).</summary>
    public const string UnionDuesRate = "compliance.unionDuesRate";

    /// <summary>
    /// ⭐ Phase 7 — tỷ lệ khấu trừ THUẾ KHOÁN của cá nhân cư trú không ký HĐLĐ / ký dưới 3 tháng
    /// (TT 111/2013 Đ.25.1.i), lấy từ <c>PitRegulationVersion.ShortTermFlatRatePercent</c> và đã
    /// chia 100 (10.00% ⇒ 0.10) như mọi tỷ lệ khác trong nhóm này.
    ///
    /// <para>⚠️ Kỳ lương cũ thiếu khoá ⇒ <c>ToFields</c> điền 0, mà tỷ lệ 0 sẽ XOÁ SẠCH thuế khoán
    /// (khác các khoá "thiếu ⇒ 0 ⇒ giữ hành vi cũ" thông thường). Vì vậy công thức THUETNCN
    /// fallback về hằng số 0.10 khi trường này = 0.</para>
    /// </summary>
    public const string ShortTermFlatRate = "compliance.shortTermFlatRate";

    // ===== Compliance — trần + giảm trừ + ngưỡng =====
    public const string BhxhBhytCap = "compliance.bhxhBhytCap";

    // ⭐ D3 — trần đóng BHYT RIÊNG (= mức tham chiếu × HealthCapMultiplier). Màn hình cấu hình BHXH
    // có ô "Hệ số trần BHYT", có validate hẳn hoi, nhưng payroll luôn dùng trần của BHXH cho cả
    // BHYT ⇒ admin sửa ô đó không có gì thay đổi.
    // ⚠️ Snapshot input của kỳ tạo TRƯỚC bản sửa không có khoá này ⇒ `ToFields` điền 0, mà
    // `InsuranceWithCap` coi cap = 0 là "không áp trần" ⇒ sẽ trả THỪA cho người lương cao.
    // Vì vậy công thức BHYT fallback về `BhxhBhytCap` khi trường này = 0.
    public const string BhytCap = "compliance.bhytCap";
    public const string BhtnCap = "compliance.bhtnCap";

    /// <summary>
    /// Trần SỐ TIỀN đoàn phí một tháng (= mức lương cơ sở × 10%, QĐ 1908/QĐ-TLĐ). Khác mọi trần
    /// khác trong nhóm này ở chỗ nó chặn trên KẾT QUẢ chứ không phải trên NỀN đóng — nên công thức
    /// PHICONGDOANVIEN dùng MIN, không dùng <c>BH_CO_TRAN</c>. 0 = không áp trần.
    /// </summary>
    public const string UnionDuesCap = "compliance.unionDuesCap";
    public const string PersonalDeduction = "compliance.personalDeduction";
    public const string DependentDeduction = "compliance.dependentDeduction";
    public const string MealExemptThreshold = "compliance.mealExemptThreshold";

    /// <summary>
    /// ⭐ Phase 7 — NGƯỠNG khấu trừ thuế khoán mỗi lần chi trả (TT 111/2013 Đ.25.1.i; NĐ 253/2026
    /// nâng 2.000.000đ → 5.000.000đ từ 01/07/2026). Tổng thu nhập DƯỚI ngưỡng ⇒ không khấu trừ.
    /// Bảng lương chi 1 lần/tháng nên "mỗi lần chi trả" = tổng thu nhập của kỳ.
    ///
    /// <para>Kỳ lương cũ thiếu khoá ⇒ 0 ⇒ không thu nhập nào dưới ngưỡng ⇒ khấu trừ như trước ✅</para>
    /// </summary>
    public const string ShortTermTaxExemptIncome = "compliance.shortTermTaxExemptIncome";
}
