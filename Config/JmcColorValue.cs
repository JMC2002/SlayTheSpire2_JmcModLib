using Godot;

namespace JmcModLib.Config;

internal static class JmcColorValue
{
    public static Color Convert(object? value)
    {
        if (value is Color color)
        {
            return color;
        }

        if (value is string text)
        {
            return Parse(text);
        }

        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return FromJsonElement(jsonElement);
        }

        return Colors.White;
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

    private static Color FromJsonElement(System.Text.Json.JsonElement jsonElement)
    {
        return jsonElement.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => Parse(jsonElement.GetString()),
            System.Text.Json.JsonValueKind.Object => FromJsonObject(jsonElement),
            _ => Colors.White
        };
    }

    private static Color FromJsonObject(System.Text.Json.JsonElement jsonElement)
    {
        float r = ReadComponent(jsonElement, "r", "R", fallback: 1f);
        float g = ReadComponent(jsonElement, "g", "G", fallback: 1f);
        float b = ReadComponent(jsonElement, "b", "B", fallback: 1f);
        float a = ReadComponent(jsonElement, "a", "A", fallback: 1f);
        return new Color(r, g, b, a);
    }

    private static float ReadComponent(System.Text.Json.JsonElement jsonElement, string lowerName, string upperName, float fallback)
    {
        if (jsonElement.TryGetProperty(lowerName, out System.Text.Json.JsonElement lower)
            && lower.TryGetSingle(out float lowerValue))
        {
            return lowerValue;
        }

        if (jsonElement.TryGetProperty(upperName, out System.Text.Json.JsonElement upper)
            && upper.TryGetSingle(out float upperValue))
        {
            return upperValue;
        }

        return fallback;
    }

    private static int ToByte(float value)
    {
        return (int)MathF.Round(Math.Clamp(value, 0f, 1f) * 255f);
    }
}

internal sealed class GodotColorJsonConverter : System.Text.Json.Serialization.JsonConverter<Color>
{
    public override Color Read(
        ref System.Text.Json.Utf8JsonReader reader,
        Type typeToConvert,
        System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.String)
        {
            return JmcColorValue.Parse(reader.GetString());
        }

        if (reader.TokenType == System.Text.Json.JsonTokenType.StartObject)
        {
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.ParseValue(ref reader);
            return JmcColorValue.Convert(document.RootElement.Clone());
        }

        return Colors.White;
    }

    public override void Write(
        System.Text.Json.Utf8JsonWriter writer,
        Color value,
        System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteStringValue(JmcColorValue.ToHex(value));
    }
}
