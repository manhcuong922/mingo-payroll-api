using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MPS.API.Serialization;

/// <summary>
/// Đảm bảo mọi <see cref="DateTime"/> đi qua JSON đều được coi là UTC.
///
/// Vấn đề: SQL Server <c>datetime2</c> không lưu <see cref="DateTimeKind"/> ⇒ EF đọc ra
/// <see cref="DateTimeKind.Unspecified"/>. System.Text.Json mặc định serialize không kèm
/// timezone suffix ⇒ FE (<c>new Date(iso)</c>) parse theo local time ⇒ lệch múi giờ.
///
/// Converter này: output ép ra ISO-8601 kết thúc bằng <c>Z</c>; input <c>Unspecified</c>
/// được coi là UTC.
/// </summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        return value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime(),
        };
        writer.WriteStringValue(utc.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture));
    }
}

/// <summary>Nullable wrapper cho <see cref="UtcDateTimeConverter"/>.</summary>
public sealed class UtcNullableDateTimeConverter : JsonConverter<DateTime?>
{
    private static readonly UtcDateTimeConverter Inner = new();

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        return Inner.Read(ref reader, typeof(DateTime), options);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else Inner.Write(writer, value.Value, options);
    }
}
