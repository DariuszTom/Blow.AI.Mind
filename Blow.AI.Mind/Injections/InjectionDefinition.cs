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
    /// <summary>Placements this injection may use. Defaults to all placements.</summary>
    public IReadOnlyList<InjectionPlacement> Placements { get; init; } = Enum.GetValues<InjectionPlacement>();

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];
}

/// <summary>Where an injection can be placed inside a workbook.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<InjectionPlacement>))]
public enum InjectionPlacement
{
    /// <summary>Visible free-text cell (the <c>Notes</c> column).</summary>
    Cell,

    /// <summary>Cell note attached to a regular data cell.</summary>
    Comment,

    /// <summary>Cell in a hidden column, written in white font.</summary>
    HiddenCell,

    /// <summary>Hidden worksheet that looks like export settings.</summary>
    HiddenSheet,

    /// <summary>Custom document property (File &gt; Info &gt; Properties).</summary>
    DocumentProperties,

    /// <summary>Workbook-level defined name holding a text constant.</summary>
    DefinedName,
}

/// <summary>How hard the injection is to spot for a human or a naive filter.</summary>
public enum Difficulty
{
    /// <summary>The instruction is written verbatim in a single location.</summary>
    Plain,

    /// <summary>
    /// The instruction is transformed (split across locations, Base64, zero-width characters or a fragmented marker).
    /// The transform is chosen randomly.
    /// </summary>
    Obfuscated,
}
