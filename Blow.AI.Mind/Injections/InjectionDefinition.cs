using System.Text.Json.Serialization;

namespace Blow.AI.Mind.Injections;

/// <summary>
/// A single prompt-injection scenario loaded from <c>Injections/&lt;category&gt;/&lt;id&gt;.json</c>.
/// </summary>
public sealed record InjectionDefinition
{
    /// <summary>Placeholder that is replaced by a unique canary marker during generation.</summary>
    public const string MarkerPlaceholder = "{{marker}}";

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("category")]
    public required string Category { get; init; }

    [JsonPropertyName("language")]
    public string Language { get; init; } = "en";

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("placements")]
    public IReadOnlyList<InjectionPlacement> Placements { get; init; } = [InjectionPlacement.Cell];

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];
}

/// <summary>Where an injection can be placed inside a workbook.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<InjectionPlacement>))]
public enum InjectionPlacement
{
    /// <summary>Visible text in a regular worksheet cell.</summary>
    Cell,
}

/// <summary>How hard the injection is to spot for a human or a naive filter.</summary>
public enum Difficulty
{
    /// <summary>The instruction is written verbatim in a single cell.</summary>
    Plain,

    /// <summary>
    /// The instruction is transformed (split across cells, Base64, zero-width characters or a fragmented marker).
    /// The transform is chosen randomly.
    /// </summary>
    Obfuscated,
}
