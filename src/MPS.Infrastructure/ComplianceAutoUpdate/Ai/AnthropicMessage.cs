using System.Text.Json;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Ai;

/// <summary>Đọc response Messages API của Anthropic.</summary>
internal static class AnthropicMessage
{
    /// <summary>
    /// Ghép mọi block <c>type="text"</c> trong <c>content</c>. Lấy cứng <c>content[0].text</c> sẽ vỡ
    /// khi model bật extended thinking (block đầu là "thinking", không có thuộc tính "text") —
    /// exception đó rơi vào catch chung và biến thành "lỗi gọi API" khó lần.
    /// </summary>
    public static string ReadText(string body)
    {
        using var dom = JsonDocument.Parse(body);
        if (!dom.RootElement.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.Array)
            return string.Empty;

        var texts = content.EnumerateArray()
            .Where(b => b.TryGetProperty("type", out var t) && t.GetString() == "text")
            .Select(b => b.TryGetProperty("text", out var v) ? v.GetString() ?? string.Empty : string.Empty);

        return string.Join("\n", texts);
    }
}
