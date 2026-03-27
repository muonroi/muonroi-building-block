using Muonroi.RuleEngine.Core.Tracing;
using System.Text;
using System.Text.Json;

namespace Muonroi.RuleEngine.Runtime.Tracing;

/// <summary>
/// Default deny-list based JSON field redactor for rule trace entries.
/// Redacts top-level PII/sensitive fields in InputFactsJson and OutputFactsJson.
/// </summary>
public sealed class DefaultTraceRedactor(IOptions<RuleTracingOptions> options) : ITraceRedactor
{
    private const string RedactedValue = "***REDACTED***";

    private readonly HashSet<string> _denyList =
        options?.Value?.RedactedFieldNames ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public RuleTraceEntry Redact(RuleTraceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        string? redactedInput = RedactJson(entry.InputFactsJson);
        string? redactedOutput = RedactJson(entry.OutputFactsJson);

        if (ReferenceEquals(redactedInput, entry.InputFactsJson) &&
            ReferenceEquals(redactedOutput, entry.OutputFactsJson))
        {
            // Nothing changed — return original entry (no allocation)
            return entry;
        }

        return entry with
        {
            InputFactsJson = redactedInput,
            OutputFactsJson = redactedOutput
        };
    }

    private string? RedactJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            // Unparseable JSON — return unchanged to avoid data loss
            return json;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return json;
            }

            bool hasRedactable = false;
            foreach (JsonProperty property in doc.RootElement.EnumerateObject())
            {
                if (_denyList.Contains(property.Name))
                {
                    hasRedactable = true;
                    break;
                }
            }

            if (!hasRedactable)
            {
                return json;
            }

            using MemoryStream ms = new();
            using Utf8JsonWriter writer = new(ms, new JsonWriterOptions { Indented = false });

            writer.WriteStartObject();
            foreach (JsonProperty property in doc.RootElement.EnumerateObject())
            {
                if (_denyList.Contains(property.Name))
                {
                    writer.WriteString(property.Name, RedactedValue);
                }
                else
                {
                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
            writer.Flush();

            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}
