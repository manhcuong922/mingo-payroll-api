using System.Text.Json;
using System.Text.Json.Serialization;

namespace MPS.Application.Common.Payroll;

/// <summary>Cấu hình JSON dùng chung cho AST công thức + cây explanation (lưu DB, trả API).</summary>
public static class PayrollJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}
