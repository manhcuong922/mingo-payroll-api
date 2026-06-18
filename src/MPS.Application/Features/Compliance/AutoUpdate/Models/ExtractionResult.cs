using System.Text.Json;
using System.Text.Json.Serialization;

namespace MPS.Application.Features.Compliance.AutoUpdate.Models;

/// <summary>Một số liệu kèm câu trích nguyên văn (evidence) phục vụ grounding.</summary>
public sealed class ExtractedValue
{
    /// <summary>Tên field logic, vd "Fund:HT:Employee", "Region:III", "Bracket:1:Rate".</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Giá trị số (tiền/tỷ lệ). Null nếu AI không tìm thấy.</summary>
    public decimal? Number { get; set; }

    /// <summary>Giá trị chuỗi (vd ThresholdMode). Null nếu không áp dụng.</summary>
    public string? Text { get; set; }

    /// <summary>Câu trích nguyên văn trong text gốc — bắt buộc cho grounding.</summary>
    public string? Evidence { get; set; }
}

/// <summary>Kết quả AI extract — JSON thuần đã parse. Mỗi số liệu kèm evidence.</summary>
public sealed class ExtractionResult
{
    public string? DocumentNo { get; set; }

    [JsonConverter(typeof(NullableDateOnlyConverter))]
    public DateOnly? EffectiveDate { get; set; }

    /// <summary>Loại entity AI xác định văn bản này thuộc về (xem TargetEntityTypes).</summary>
    public string? TargetEntityType { get; set; }

    /// <summary>Danh sách field bóc được (phẳng, dùng chung cho mọi entity type).</summary>
    public List<ExtractedValue> Values { get; set; } = new();

    /// <summary>JSON thô AI trả về (lưu vào proposal.ExtractedJson).</summary>
    [JsonIgnore]
    public string RawJson { get; set; } = string.Empty;
}

/// <summary>Converter cho DateOnly? để AI có thể trả "2026-07-01" hoặc null.</summary>
public sealed class NullableDateOnlyConverter : JsonConverter<DateOnly?>
{
    public override DateOnly? Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var s = reader.GetString();
        return DateOnly.TryParse(s, out var d) ? d : null;
    }

    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions o)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd"));
    }
}
