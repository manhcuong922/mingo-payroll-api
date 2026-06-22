namespace MPS.Infrastructure.ComplianceAutoUpdate.Ai;

/// <summary>Prompt yêu cầu AI bóc số liệu — JSON thuần, mỗi field kèm evidence.</summary>
public static class ExtractionPrompt
{
    public const string System =
        "Bạn là trợ lý trích xuất số liệu pháp luật lao động/thuế Việt Nam. " +
        "CHỈ trích những gì CÓ trong văn bản, KHÔNG suy luận, KHÔNG bịa. " +
        "Không tìm thấy thì để null. Trả về JSON THUẦN, KHÔNG markdown, KHÔNG giải thích.";

    public static string BuildUser(string scopedText) =>
        $$"""
        Trích các số liệu thuộc phạm vi: BHXH/BHYT/BHTN/kinh phí công đoàn, lương tối thiểu vùng,
        biểu thuế TNCN (giảm trừ + bậc), ngưỡng miễn phụ cấp, hằng số lao động.

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
          "EmployerUnemploymentPercent", "EmployerUnionFundPercent", "ReferenceAmount".
        - Lương tối thiểu (MinimumWageVersion): "Region:I", "Region:II", "Region:III", "Region:IV".
        - Thuế TNCN (PitRegulationVersion): "PersonalDeductionAmount", "DependentDeductionAmount",
          và mỗi bậc "Bracket:{số thứ tự}:Rate" (thuế suất dạng số thập phân, vd 0.05 cho 5%).
        - Ngưỡng miễn phụ cấp (AllowanceExemptThresholdVersion): "MonthlyExemptAmount", "ThresholdMode" (text).

        BẮT BUỘC: mỗi value phải có "evidence" là câu trích NGUYÊN VĂN (copy đúng từ văn bản),
        để hệ thống đối chiếu chống bịa số.

        Văn bản:
        ---
        {{scopedText}}
        ---
        """;
}
