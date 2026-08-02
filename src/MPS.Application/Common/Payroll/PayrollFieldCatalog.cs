using MPS.Domain.Enums.Payroll;
using static MPS.Application.Common.Payroll.PayrollFieldKeys;

namespace MPS.Application.Common.Payroll;

/// <summary>Danh mục field cố định (whitelist). Nhãn tiếng Việt để hiển thị + dựng explanation.</summary>
public sealed class PayrollFieldCatalog : IPayrollFieldCatalog
{
    private static readonly PayrollFieldDescriptor[] Fields =
    {
        // Hồ sơ
        new(EmployeeCode, "Mã nhân viên", "Hồ sơ", ColumnDataType.Text),
        new(EmployeeFullName, "Họ và tên", "Hồ sơ", ColumnDataType.Text),
        new(EmployeePosition, "Chức vụ", "Hồ sơ", ColumnDataType.Text),
        new(EmployeeBankAccount, "Số tài khoản", "Hồ sơ", ColumnDataType.Text),
        new(EmployeeJoinDate, "Ngày vào", "Hồ sơ", ColumnDataType.Text),
        new(PeriodMonthLabel, "Tháng lương", "Kỳ lương", ColumnDataType.Text),
        new(DependentsCount, "Số người phụ thuộc", "Hồ sơ", ColumnDataType.Number),

        // Hợp đồng
        new(ContractMucLuong, "Mức lương hợp đồng", "Hợp đồng", ColumnDataType.Money),
        new(ContractInsuranceFlag, "Cờ bảo hiểm (mặc định HĐ)", "Hợp đồng", ColumnDataType.Text),
        new(ContractTaxFlag, "Cờ thuế (mặc định HĐ)", "Hợp đồng", ColumnDataType.Text),
        new(ContractNoSocialInsurance, "HĐ miễn BHXH (1 = miễn)", "Hợp đồng", ColumnDataType.Number),
        new(ContractNoHealthInsurance, "HĐ miễn BHYT (1 = miễn)", "Hợp đồng", ColumnDataType.Number),
        new(ContractNoUnemploymentInsurance, "HĐ miễn BHTN (1 = miễn)", "Hợp đồng", ColumnDataType.Number),
        new(ContractAllowanceThueNha, "Phụ cấp thuê nhà (HĐ)", "Hợp đồng", ColumnDataType.Money),
        new(ContractAllowanceDienThoai, "Phụ cấp điện thoại (HĐ)", "Hợp đồng", ColumnDataType.Money),
        new(ContractAllowanceXangXe, "Phụ cấp xăng xe (HĐ)", "Hợp đồng", ColumnDataType.Money),
        new(ContractAllowanceCom, "Phụ cấp cơm/ăn ca (HĐ)", "Hợp đồng", ColumnDataType.Money),
        new(ContractAllowanceKhac, "Phụ cấp khác (HĐ, mã ngoài 4 cột cứng)", "Hợp đồng", ColumnDataType.Money),
        new(ContractAllowanceNgoaiLe, "Phụ cấp ngoại lệ — trả nguyên, không chia theo công (HĐ)", "Hợp đồng", ColumnDataType.Money),
        new(ContractAllowanceComNgoaiLe, "Phụ cấp cơm ngoại lệ — trả nguyên (HĐ)", "Hợp đồng", ColumnDataType.Money),

        // Chấm công
        new(CongChuan, "Công chuẩn tháng", "Kỳ lương", ColumnDataType.Number),
        new(CongTinhCom, "Công tính cơm", "Chấm công", ColumnDataType.Number),
        new(CongThuViec, "Công thử việc", "Chấm công", ColumnDataType.Number),
        new(CongChinhThuc, "Công chính thức", "Chấm công", ColumnDataType.Number),
        new(CongHocViec, "Công học việc", "Chấm công", ColumnDataType.Number),
        new(DayHours, "Giờ ban ngày", "Chấm công", ColumnDataType.Number),
        new(NightHours, "Giờ ban đêm", "Chấm công", ColumnDataType.Number),
        new(AbsentDays, "Số ngày vắng", "Chấm công", ColumnDataType.Number),
        new(LateDays, "Số lần muộn/sớm", "Chấm công", ColumnDataType.Number),

        // Tăng ca
        new(OtTangCa, "Tiền tăng ca", "Tăng ca", ColumnDataType.Money),
        new(OtChenhLech, "Chênh lệch làm thêm giờ (miễn thuế)", "Tăng ca", ColumnDataType.Money),

        // Phụ trội giờ thường theo loại ngày / ca đêm (BS-2)
        new(PremiumDayType, "Phụ trội ngày nghỉ/lễ (miễn thuế)", "Phụ trội", ColumnDataType.Money),
        new(PremiumNight, "Phụ cấp làm đêm (miễn thuế)", "Phụ trội", ColumnDataType.Money),
        new(WeeklyRestHours, "Giờ làm ngày nghỉ tuần", "Chấm công", ColumnDataType.Number),
        new(HolidayHours, "Giờ làm ngày lễ", "Chấm công", ColumnDataType.Number),

        // Đánh giá
        new(EvalFinalScore, "Điểm đánh giá tháng", "Đánh giá", ColumnDataType.Number),
        new(ProductivityCoefficient, "Hệ số năng suất", "Đánh giá", ColumnDataType.Number),

        // Truy lĩnh / truy thu (BS-5)
        new(TruyLinh, "Truy lĩnh kỳ trước", "Truy lĩnh", ColumnDataType.Money),
        new(TruyThu, "Truy thu kỳ trước", "Truy lĩnh", ColumnDataType.Money),

        // Thưởng / công đoàn
        new(BonusThuongDuAn, "Thưởng dự án", "Thưởng", ColumnDataType.Money),
        new(UnionFee, "Phí công đoàn viên", "Công đoàn", ColumnDataType.Money),

        // Compliance — tỷ lệ
        new(BhxhDnRate, "Tỷ lệ BHXH – DN", "Compliance", ColumnDataType.Percent),
        new(BhxhNldRate, "Tỷ lệ BHXH – NLĐ", "Compliance", ColumnDataType.Percent),
        new(BhytDnRate, "Tỷ lệ BHYT – DN", "Compliance", ColumnDataType.Percent),
        new(BhytNldRate, "Tỷ lệ BHYT – NLĐ", "Compliance", ColumnDataType.Percent),
        new(BhtnDnRate, "Tỷ lệ BHTN – DN", "Compliance", ColumnDataType.Percent),
        new(BhtnNldRate, "Tỷ lệ BHTN – NLĐ", "Compliance", ColumnDataType.Percent),
        new(KpcdRate, "Tỷ lệ KPCĐ – DN", "Compliance", ColumnDataType.Percent),

        // Compliance — trần + giảm trừ
        new(BhxhBhytCap, "Trần đóng BHXH", "Compliance", ColumnDataType.Money),
        new(BhytCap, "Trần đóng BHYT", "Compliance", ColumnDataType.Money),
        new(BhtnCap, "Trần đóng BHTN", "Compliance", ColumnDataType.Money),
        new(PersonalDeduction, "Giảm trừ bản thân", "Compliance", ColumnDataType.Money),
        new(DependentDeduction, "Giảm trừ mỗi người phụ thuộc", "Compliance", ColumnDataType.Money),
        new(MealExemptThreshold, "Mức cơm miễn thuế tối đa", "Compliance", ColumnDataType.Money),
    };

    private static readonly Dictionary<string, PayrollFieldDescriptor> ByKey =
        Fields.ToDictionary(f => f.Key, StringComparer.Ordinal);

    public IReadOnlyList<PayrollFieldDescriptor> GetAll() => Fields;

    public bool TryGet(string key, out PayrollFieldDescriptor descriptor) => ByKey.TryGetValue(key, out descriptor!);

    public string LabelOf(string key) => ByKey.TryGetValue(key, out var d) ? d.Label : key;
}
