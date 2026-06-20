using MPS.Domain.Entities.Compliance.AutoUpdate;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Ai;

/// <summary>Prompt cho Kênh B: hỏi LLM (kèm web search) xem có văn bản chi phối mới hơn không.</summary>
public static class WatcherPrompt
{
    public const string System =
        "Bạn là trợ lý rà soát pháp luật lao động/thuế Việt Nam. Dùng web search để kiểm tra " +
        "văn bản CHÍNH THỨC mới nhất đang có hiệu lực. TUYỆT ĐỐI không bịa số hiệu hay URL — " +
        "chỉ nêu URL nguồn gốc thật (Chính phủ, Công báo, vbpl.vn, thuvienphapluat.vn...). " +
        "Trả về JSON THUẦN, không markdown, không giải thích ngoài JSON.";

    private static readonly Dictionary<string, string> EntityDesc = new()
    {
        [TargetEntityTypes.SocialInsuranceRateVersion] = "tỷ lệ đóng BHXH/BHYT/BHTN/kinh phí công đoàn",
        [TargetEntityTypes.MinimumWageVersion]         = "mức lương tối thiểu vùng",
        [TargetEntityTypes.PitRegulationVersion]       = "biểu thuế thu nhập cá nhân (giảm trừ + bậc)",
        [TargetEntityTypes.LaborLawConstantsVersion]   = "hằng số luật lao động (giờ làm, làm thêm, nghỉ phép...)",
        [TargetEntityTypes.AllowanceExemptThresholdVersion] = "ngưỡng miễn thuế của phụ cấp",
    };

    public static string BuildUser(string targetEntityType, string? currentDocumentNo)
    {
        var desc = EntityDesc.TryGetValue(targetEntityType, out var d) ? d : targetEntityType;
        var known = string.IsNullOrWhiteSpace(currentDocumentNo)
            ? "Hệ thống chưa ghi nhận văn bản đang áp dụng."
            : $"Văn bản hệ thống đang áp dụng: {currentDocumentNo}.";

        return $$"""
        Chủ đề: {{desc}} của Việt Nam.
        {{known}}

        Dùng web search để xác định: hiện có văn bản CHÍNH THỨC nào MỚI HƠN văn bản đang áp dụng,
        đã hoặc sắp có hiệu lực, thay đổi nội dung này không?

        Trả JSON đúng shape:
        {
          "hasNewer": true/false,
          "documentNo": "số hiệu văn bản mới (vd 74/2024/NĐ-CP) hoặc null",
          "effectiveDate": "yyyy-MM-dd hoặc null",
          "sourceUrl": "URL trang nguồn gốc chứa toàn văn (KHÔNG bịa) hoặc null",
          "summary": "1-2 câu nêu phát hiện và căn cứ"
        }

        Nếu không chắc chắn có bản mới hơn, đặt hasNewer=false.
        """;
    }
}
