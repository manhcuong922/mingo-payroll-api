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
    public const string ContractInsuranceFlag = "contract.insuranceFlag"; // text
    public const string ContractTaxFlag = "contract.taxFlag";             // text
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
    public const string DayHours = "attendance.dayHours";
    public const string NightHours = "attendance.nightHours";
    public const string AbsentDays = "attendance.absentDays";
    public const string LateDays = "attendance.lateDays";

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

    // ===== Thưởng / công đoàn / người phụ thuộc =====
    public const string BonusThuongDuAn = "bonus.thuongDuAn";
    public const string DependentsCount = "employee.dependentsCount";
    public const string UnionFee = "union.phiCongDoanVien";

    // ===== Compliance — tỷ lệ (phân số) =====
    public const string BhxhDnRate = "compliance.bhxhDnRate";
    public const string BhxhNldRate = "compliance.bhxhNldRate";
    public const string BhytDnRate = "compliance.bhytDnRate";
    public const string BhytNldRate = "compliance.bhytNldRate";
    public const string BhtnDnRate = "compliance.bhtnDnRate";
    public const string BhtnNldRate = "compliance.bhtnNldRate";
    public const string KpcdRate = "compliance.kpcdRate";

    // ===== Compliance — trần + giảm trừ + ngưỡng =====
    public const string BhxhBhytCap = "compliance.bhxhBhytCap";
    public const string BhtnCap = "compliance.bhtnCap";
    public const string PersonalDeduction = "compliance.personalDeduction";
    public const string DependentDeduction = "compliance.dependentDeduction";
    public const string MealExemptThreshold = "compliance.mealExemptThreshold";
}
