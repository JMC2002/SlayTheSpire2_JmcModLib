using Godot;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JmcModLib.Config.Serialization;

internal static class JmcColorValue
{
    public static Color Convert(object? value)
    {
        return value switch
        {
            Color color => color,
            string text => Parse(text),
            JsonElement jsonElement => FromJsonElement(jsonElement),
            _ => Colors.White
        };
    }

    public static Color Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Colors.White;
        }

        string normalized = text.Trim().TrimStart('#');
        return new Color(normalized);
    }

    public static string ToHex(Color color, bool includeAlpha = true)
    {
        int r = ToByte(color.R);
        int g = ToByte(color.G);
        int b = ToByte(color.B);
        int a = ToByte(color.A);
        return includeAlpha
            ? $"#{r:X2}{g:X2}{b:X2}{a:X2}"
            : $"#{r:X2}{g:X2}{b:X2}";
    }

    private static int ToByte(float value)
    {
        return (int)MathF.Round(Math.Clamp(value, 0f, 1f) * 255f);
    }

    private static Color FromJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => Parse(element.GetString()),
            JsonValueKind.Object => ReadObject(element),
            _ => Colors.White
        };
    }

    private static Color ReadObject(JsonElement element)
    {
        float r = ReadFloat(element, "r", fallback: 1f);
        float g = ReadFloat(element, "g", fallback: 1f);
        float b = ReadFloat(element, "b", fallback: 1f);
        float a = ReadFloat(element, "a", fallback: 1f);
        return new Color(r, g, b, a);
    }

    private static float ReadFloat(JsonElement element, string propertyName, float fallback)
    {
        if (!TryGetProperty(element, propertyName, out JsonElement property))
        {
            return fallback;
        }

        try
        {
            return property.ValueKind switch
            {
                JsonValueKind.Number when property.TryGetDouble(out double number) => (float)number,
                JsonValueKind.String when float.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) => parsed,
                _ => fallback
            };
        }
        catch
        {
            return fallback;
        }
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.TryGetProperty(propertyName, out property))
        {
            return true;
        }

        string upperName = propertyName.ToUpperInvariant();
        return element.TryGetProperty(upperName, out property);
    }
}

internal sealed class GodotColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => JmcColorValue.Parse(reader.GetString()),
            JsonTokenType.StartObject => ReadJsonObject(ref reader),
            JsonTokenType.Null => Colors.White,
            _ => Colors.White
        };
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(JmcColorValue.ToHex(value));
    }

    private static Color ReadJsonObject(ref Utf8JsonReader reader)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        return JmcColorValue.Convert(document.RootElement);
    }
}
