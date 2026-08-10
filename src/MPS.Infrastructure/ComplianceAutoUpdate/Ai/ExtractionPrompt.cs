namespace MPS.Infrastructure.ComplianceAutoUpdate.Ai;

/// <summary>
/// Prompt yêu cầu AI bóc số liệu — JSON thuần, mỗi field kèm evidence.
///
/// Tên field PHẢI khớp đúng convention mà <c>ComplianceDiffService</c> dùng để flatten data active;
/// thiếu một nhóm entity trong danh sách dưới đây là AI không có tên để trả, và mọi field của nhóm đó
/// rơi hết vào NotExtracted → proposal vô nghĩa. Giữ danh sách này đồng bộ với
/// <c>ComplianceDiffService.LoadActiveAsync</c> và <c>ComplianceProposalApplier</c>.
/// </summary>
public static class ExtractionPrompt
{
    public const string System =
        "Bạn là trợ lý trích xuất số liệu pháp luật lao động/thuế Việt Nam. " +
        "CHỈ trích những gì CÓ trong văn bản, KHÔNG suy luận, KHÔNG bịa. " +
        "Không tìm thấy thì để null. Trả về JSON THUẦN, KHÔNG markdown, KHÔNG giải thích.";

    public static string BuildUser(string scopedText) => BuildUser(scopedText, 1, 1);

    /// <param name="part">Số thứ tự phần (1-based) khi văn bản dài phải chia nhiều lượt gọi.</param>
    /// <param name="total">Tổng số phần. 1 = gửi trọn một lượt.</param>
    public static string BuildUser(string scopedText, int part, int total) =>
        $$"""
        Trích các số liệu thuộc phạm vi: BHXH/BHYT/BHTN/kinh phí công đoàn, lương tối thiểu vùng,
        biểu thuế TNCN (giảm trừ + bậc), ngưỡng miễn phụ cấp, hằng số lao động.
        {{PartNote(part, total)}}
        Trả JSON đúng shape sau:
        {
          "documentNo": "string hoặc null",
          "effectiveDate": "yyyy-MM-dd hoặc null",
          "targetEntityType": "một trong: SocialInsuranceRateVersion | MinimumWageVersion | PitRegulationVersion | LaborLawConstantsVersion | AllowanceExemptThresholdVersion",
          "values": [
            { "field": "tên field",
              "number": 0,
              "text": "chuỗi hoặc null",
              "evidence": "CÂU TRÍCH NGUYÊN VĂN trong văn bản chứa con số này" }
          ]
        }

        Convention tên field (BẮT BUỘC khớp đúng để hệ thống đối chiếu):
        - BHXH (SocialInsuranceRateVersion): các quỹ thành phần dùng "Fund:{MãQuỹ}:Employee" và "Fund:{MãQuỹ}:Employer"
          với mã quỹ: HT (hưu trí, tử tuất), OdTs (ốm đau, thai sản), TnldBnn (tai nạn lao động, bệnh nghề nghiệp).
          Các tỷ lệ khác: "EmployeeHealthPercent", "EmployerHealthPercent", "EmployeeUnemploymentPercent",
          "EmployerUnemploymentPercent", "EmployerUnionFundPercent", "EmployeeUnionDuesPercent",
          "EmployeeUnionDuesCapPercent", "ReferenceAmount".
          Tỷ lệ ghi dạng SỐ PHẦN TRĂM (vd 8 cho 8%); "ReferenceAmount" ghi số tiền đồng.
        - Lương tối thiểu (MinimumWageVersion): "Region:I", "Region:II", "Region:III", "Region:IV" —
          số tiền THÁNG, đơn vị đồng (vd 4960000).
        - Thuế TNCN (PitRegulationVersion): "PersonalDeductionAmount", "DependentDeductionAmount"
          (số tiền THÁNG, đơn vị đồng — "15,5 triệu đồng/tháng" ghi 15500000),
          và mỗi bậc "Bracket:{số thứ tự}:Rate" (thuế suất dạng số thập phân, vd 0.05 cho 5%).
        - Hằng số lao động (LaborLawConstantsVersion) — ghi dạng SỐ PHẦN TRĂM cho các field *Percent/*Floor
          (vd 85 cho 85%, KHÔNG ghi 0.85):
          "ProbationSalaryMinPercent" (lương thử việc tối thiểu, % — Đ.26, vd 85),
          "OvertimeNightExtraPercent" (phụ trội làm thêm vào ban đêm, % — Đ.98.2, vd 20),
          "NightWorkSurchargePercent" (phụ cấp làm việc ban đêm, % — Đ.98.2, vd 30),
          "WorkRateNormalFloor" (đơn giá NGÀY THƯỜNG làm mốc, % — vd 100),
          "OvertimeNormalUpliftPercent" (phần TRỘI THÊM khi làm thêm ngày thường, % — Đ.98.1.a
            quy định "ít nhất 150%" nghĩa là mốc 100 cộng trội 50 → ghi 50, KHÔNG ghi 150),
          "WorkRateWeeklyRestFloor" (làm thêm ngày nghỉ hằng tuần, % — Đ.98.1.b, vd 200),
          "WorkRateHolidayFloor" (làm thêm ngày lễ/tết, % — Đ.98.1.c, vd 300),
          "StandardWorkingHoursPerDay" (giờ/ngày), "MaxRegularHoursPerWeek" (giờ/tuần),
          "StandardWorkingDaysPerMonth" (ngày/tháng), "AnnualLeaveMinDays" (ngày phép năm tối thiểu),
          "OvertimeMaxHoursPerDay" (giờ), "OvertimeMaxHoursPerMonth" (giờ), "OvertimeMaxHoursPerYear" (giờ).
        - Ngưỡng miễn phụ cấp (AllowanceExemptThresholdVersion): "MonthlyExemptAmount" (đồng/tháng),
          "ThresholdMode" (text).

        BẮT BUỘC: mỗi value phải có "evidence" là câu trích NGUYÊN VĂN (copy đúng từ văn bản),
        để hệ thống đối chiếu chống bịa số.

        Văn bản:
        ---
        {{scopedText}}
        ---
        """;

    /// <summary>
    /// Nhắc model rằng nó chỉ thấy một lát cắt — để nó im lặng bỏ qua field không có trong phần này
    /// thay vì cố suy đoán từ trí nhớ.
    /// </summary>
    private static string PartNote(int part, int total) => total <= 1
        ? string.Empty
        : $"""

          LƯU Ý: đây là PHẦN {part}/{total} của một văn bản dài, đã cắt theo ranh giới điều khoản.
          Chỉ trích số liệu CÓ TRONG PHẦN NÀY. Field thuộc phần khác thì bỏ qua, KHÔNG đoán.

          """;
}
