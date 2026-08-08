using System.Globalization;
using System.Text.Json;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;

namespace MPS.Infrastructure.Services;

/// <summary>
/// Parse + map dữ liệu CCCD dùng chung cho mọi provider OCR.
///
/// Contract JSON ở đây là schema của FPT.AI. Provider mới (Anthropic) được ép trả về
/// ĐÚNG bộ tên field này, nhờ vậy output của <c>/api/ocr/citizen-id</c> không đổi khi
/// hoán provider — FE không phải sửa gì.
/// </summary>
public static class CitizenIdOcrParsing
{
    /// <summary>Loại thẻ hợp lệ ở mặt trước, gồm cả CMND cũ và thẻ Căn cước 2024+.</summary>
    public static readonly IReadOnlySet<string> ValidFrontDocTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // CMND (cũ) + CCCD mã vạch
        "cmnd_front", "cccd_front",
        // CCCD gắn chip (2021+)
        "chip_front",
        // Thẻ Căn cước mới theo Luật Căn cước 2023 (hiệu lực 01/07/2024)
        "cc_front"
    };

    public static readonly IReadOnlySet<string> ValidBackDocTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "cmnd_back", "cccd_back", "chip_back", "cc_back"
    };

    /// <summary>
    /// Map JSON mặt trước → <see cref="CitizenIdOcrResult"/>.
    /// Throw <see cref="OcrInvalidDocumentTypeException"/> nếu ảnh không phải giấy tờ tùy thân VN.
    /// </summary>
    public static CitizenIdOcrResult ParseFront(JsonElement data)
    {
        var docType = GetString(data, "type");
        if (docType is null || !ValidFrontDocTypes.Contains(docType))
            throw new OcrInvalidDocumentTypeException(docType);

        DateTime? dob = null;
        if (GetString(data, "dob") is { } dobStr)
            dob = TryParseDateVn(dobStr);

        return new CitizenIdOcrResult(
            FullName: GetString(data, "name"),
            Dob: dob,
            Gender: MapGender(GetString(data, "sex")),
            CitizenId: GetString(data, "id"),
            Nationality: GetString(data, "nationality"),
            PermanentAddress: GetString(data, "home"),
            NativePlaceAddress: GetString(data, "home"),      // Quê quán
            ResidenceAddress: GetString(data, "address"),     // Nơi thường trú
            Type: docType,
            RawConfidence: GetDouble(data, "confidence")
        );
    }

    /// <summary>
    /// Map JSON mặt sau → <see cref="CitizenIdBackOcrResult"/>.
    /// Throw <see cref="OcrInvalidDocumentTypeException"/> nếu ảnh không phải mặt sau thẻ.
    /// </summary>
    public static CitizenIdBackOcrResult ParseBack(JsonElement data)
    {
        var docType = GetString(data, "type");
        if (docType is null || !ValidBackDocTypes.Contains(docType))
            throw new OcrInvalidDocumentTypeException(docType);

        DateTime? issuedDate = null;
        if (GetString(data, "issue_date") is { } issueStr)
            issuedDate = TryParseDateVn(issueStr);

        // FPT đặt tên field không nhất quán giữa các phiên bản (cccd_back / chip_back / cc_back) → thử lần lượt.
        var issuedPlace = GetFirstString(data, "issue_loc", "issue_place", "issued_at", "place_of_issue");

        // Thẻ Căn cước mới (cc_back): "nơi cư trú" và "nơi đăng ký khai sinh" nằm ở mặt sau (Luật Căn cước 2023).
        var residenceAddress = GetFirstString(data, "place_of_residence", "residence", "address", "home");
        var placeOfBirthRegistration = GetFirstString(data, "place_of_birth_registration", "place_of_birth", "birth_place", "pob");

        return new CitizenIdBackOcrResult(
            issuedDate,
            issuedPlace,
            residenceAddress,
            placeOfBirthRegistration,
            GetDouble(data, "confidence"));
    }

    /// <summary>
    /// Đọc string, coi chuỗi rỗng/trắng là null.
    /// Claude trả "" cho field không đọc được, FPT bỏ hẳn key — chuẩn hoá về cùng một dạng null.
    /// </summary>
    public static string? GetString(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var p) || p.ValueKind != JsonValueKind.String) return null;
        var val = p.GetString();
        return string.IsNullOrWhiteSpace(val) ? null : val;
    }

    public static string? GetFirstString(JsonElement el, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (GetString(el, key) is { } val) return val;
        }
        return null;
    }

    private static double? GetDouble(JsonElement el, string key) =>
        el.TryGetProperty(key, out var p) && p.TryGetDouble(out var val) ? val : null;

    public static DateTime? TryParseDateVn(string s)
    {
        if (DateTime.TryParseExact(s, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return DateTime.SpecifyKind(d, DateTimeKind.Utc);
        return null;
    }

    public static string? MapGender(string? sex) =>
        sex?.ToUpperInvariant() switch
        {
            "NAM" => "Male",
            "NỮ" or "NU" => "Female",
            _ => sex
        };
}
