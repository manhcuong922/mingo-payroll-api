using System.Text.Json;
using System.Text.RegularExpressions;
using MPS.Application.Features.Compliance.AutoUpdate.Models;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Ai;

/// <summary>Parse JSON AI trả về thành <see cref="ExtractionResult"/>, an toàn với fence markdown.</summary>
public static class JsonExtraction
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Bỏ ```json ... ``` fence nếu model vẫn thêm.</summary>
    public static string StripFences(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var s = raw.Trim();
        var m = Regex.Match(s, "```(?:json)?\\s*(.*?)```", RegexOptions.Singleline);
        if (m.Success) return m.Groups[1].Value.Trim();
        return s;
    }

    /// <summary>Parse; lỗi → trả null (caller mark Failed).</summary>
    public static ExtractionResult? TryParse(string raw)
    {
        var json = StripFences(raw);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var result = JsonSerializer.Deserialize<ExtractionResult>(json, Options);
            if (result == null) return null;
            result.RawJson = json;
            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
