using System.Text.Json;
using System.Text.Json.Serialization;

namespace Blueline.Ingestion.Nhl;

/// <summary>
/// Reads a <see cref="LocalizedText"/> from either shape the league API uses for the same field.
///
/// Team abbreviations arrive as a bare string from /score and /club-schedule-season, but as
/// { "default": "CHI" } from /boxscore and /standings. Rather than modelling one field two ways
/// per endpoint, every localized field goes through this converter.
/// </summary>
public class LocalizedTextConverter : JsonConverter<LocalizedText>
{
    public override LocalizedText? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.String:
                return new LocalizedText(reader.GetString() ?? "");

            case JsonTokenType.StartObject:
            {
                string? value = null;
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject) break;
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;

                    var property = reader.GetString();
                    reader.Read();

                    // Take "default" and ignore the other locales.
                    if (string.Equals(property, "default", StringComparison.OrdinalIgnoreCase)
                        && reader.TokenType == JsonTokenType.String)
                    {
                        value = reader.GetString();
                    }
                    else if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                    {
                        // Skip() throws when the serializer is reading from a stream and the value
                        // straddles a buffer boundary; TrySkip degrades instead of failing the parse.
                        // Scalar values need no skipping — the reader is already sitting on them.
                        reader.TrySkip();
                    }
                }
                return new LocalizedText(value ?? "");
            }

            default:
                reader.TrySkip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, LocalizedText value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Default);
}
