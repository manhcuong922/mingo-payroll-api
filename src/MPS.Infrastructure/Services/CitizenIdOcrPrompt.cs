namespace MPS.Infrastructure.Services;

/// <summary>
/// System prompt + JSON schema cho OCR CCCD bằng Claude vision.
///
/// Tên field trong schema cố ý trùng schema FPT.AI (name/id/dob/sex/home/address/type/issue_date/issue_loc)
/// để <see cref="CitizenIdOcrParsing"/> dùng chung cho cả hai provider.
/// </summary>
internal static class CitizenIdOcrPrompt
{
    public const string FrontSystem = """
        Bạn trích xuất dữ liệu từ ảnh giấy tờ tùy thân Việt Nam. Đọc ảnh và trả về JSON đúng schema được yêu cầu.

        Quy tắc đọc:
        - Chép nguyên văn chữ in trên thẻ: giữ đủ dấu tiếng Việt và cách viết hoa như bản gốc.
        - Không suy đoán, không sửa chính tả, không tự chuẩn hoá địa danh. Đọc được gì ghi đúng như thế.
        - Field không đọc được, bị che, hoặc thẻ không có field đó: trả chuỗi rỗng "".
        - dob: định dạng dd/MM/yyyy. Nếu trên thẻ ghi thiếu ngày hoặc tháng thì trả "".
        - id: chỉ chữ số, bỏ mọi khoảng trắng.
        - sex: đúng một trong hai giá trị "NAM" hoặc "NỮ".

        Xác định type theo mặt trước đang thấy:
        - "cmnd_front"  — CMND 9 số, tiêu đề "GIẤY CHỨNG MINH NHÂN DÂN"
        - "cccd_front"  — CCCD mã vạch (2016–2021), tiêu đề "CĂN CƯỚC CÔNG DÂN", không có chip
        - "chip_front"  — CCCD gắn chip (2021+), tiêu đề "CĂN CƯỚC CÔNG DÂN", có chip vàng ở mặt trước
        - "cc_front"    — thẻ Căn cước mới (từ 01/07/2024), tiêu đề "CĂN CƯỚC"
        - "not_id_card" — ảnh không phải giấy tờ tùy thân Việt Nam, hoặc đang là MẶT SAU của thẻ,
                          hoặc ảnh mờ/tối tới mức không đọc được thông tin nào

        Ánh xạ nhãn trên thẻ sang field:
        - home    = "Quê quán" (thẻ Căn cước mới: "Nơi đăng ký khai sinh")
        - address = "Nơi thường trú" (thẻ Căn cước mới: "Nơi cư trú")

        Khi type = "not_id_card", để tất cả field còn lại là "".
        """;

    public const string BackSystem = """
        Bạn trích xuất dữ liệu từ ảnh MẶT SAU giấy tờ tùy thân Việt Nam. Đọc ảnh và trả về JSON đúng schema được yêu cầu.

        Quy tắc đọc:
        - Chép nguyên văn chữ in trên thẻ: giữ đủ dấu tiếng Việt và cách viết hoa như bản gốc.
        - Không suy đoán, không sửa chính tả. Field không đọc được hoặc thẻ không có: trả chuỗi rỗng "".
        - issue_date: định dạng dd/MM/yyyy, lấy từ dòng "Ngày, tháng, năm" cấp.

        Xác định type theo mặt sau đang thấy:
        - "cmnd_back"   — mặt sau CMND 9 số
        - "cccd_back"   — mặt sau CCCD mã vạch (có mã vạch đen dài)
        - "chip_back"   — mặt sau CCCD gắn chip (có MRZ ba dòng ký tự "<<<" ở đáy thẻ)
        - "cc_back"     — mặt sau thẻ Căn cước mới (từ 01/07/2024), có thêm "Nơi cư trú" và "Nơi đăng ký khai sinh"
        - "not_id_card" — ảnh không phải giấy tờ tùy thân Việt Nam, hoặc đang là MẶT TRƯỚC của thẻ,
                          hoặc ảnh mờ/tối tới mức không đọc được thông tin nào

        Ánh xạ nhãn trên thẻ sang field:
        - issue_loc                   = cơ quan cấp (vd "CỤC TRƯỞNG CỤC CẢNH SÁT QUẢN LÝ HÀNH CHÍNH VỀ TRẬT TỰ XÃ HỘI")
        - place_of_residence          = "Nơi cư trú" — chỉ có trên cc_back
        - place_of_birth_registration = "Nơi đăng ký khai sinh" — chỉ có trên cc_back

        Khi type = "not_id_card", để tất cả field còn lại là "".
        """;

    /// <summary>
    /// Nhắc lại yêu cầu JSON ở user turn. Thừa khi structured outputs hoạt động, nhưng là lưới an toàn
    /// nếu proxy nuốt mất tham số output_config — khi đó model vẫn trả JSON theo prompt.
    /// </summary>
    public const string User = "Trích xuất thông tin từ ảnh này. Chỉ trả về một object JSON, không kèm giải thích hay markdown.";

    /// <summary>Schema mặt trước — trùng tên field với response FPT.AI.</summary>
    public static object FrontSchema() => new
    {
        type = "object",
        properties = new
        {
            type = Enum_("cmnd_front", "cccd_front", "chip_front", "cc_front", "not_id_card"),
            name = Str("Họ và tên, in hoa như trên thẻ"),
            id = Str("Số CCCD/CMND, chỉ chữ số"),
            dob = Str("Ngày sinh dd/MM/yyyy"),
            sex = Enum_("NAM", "NỮ", ""),
            nationality = Str("Quốc tịch, vd Việt Nam"),
            home = Str("Quê quán / Nơi đăng ký khai sinh"),
            address = Str("Nơi thường trú / Nơi cư trú"),
        },
        required = new[] { "type", "name", "id", "dob", "sex", "nationality", "home", "address" },
        additionalProperties = false,
    };

    /// <summary>Schema mặt sau — trùng tên field với response FPT.AI.</summary>
    public static object BackSchema() => new
    {
        type = "object",
        properties = new
        {
            type = Enum_("cmnd_back", "cccd_back", "chip_back", "cc_back", "not_id_card"),
            issue_date = Str("Ngày cấp dd/MM/yyyy"),
            issue_loc = Str("Cơ quan cấp"),
            place_of_residence = Str("Nơi cư trú — chỉ thẻ Căn cước mới"),
            place_of_birth_registration = Str("Nơi đăng ký khai sinh — chỉ thẻ Căn cước mới"),
        },
        required = new[] { "type", "issue_date", "issue_loc", "place_of_residence", "place_of_birth_registration" },
        additionalProperties = false,
    };

    private static object Str(string description) => new { type = "string", description };

    private static object Enum_(params string[] values) => new { type = "string", @enum = values };
}
