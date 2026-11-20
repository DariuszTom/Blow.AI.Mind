namespace Blow.AI.Mind.Injections;

/// <summary>Filters and settings for injection selection.</summary>
public sealed class InjectionOptions
{
    /// <summary>Difficulty of the injected text. Defaults to <see cref="Mind.Injections.Difficulty.Obfuscated"/>.</summary>
    public Difficulty Difficulty { get; set; } = Difficulty.Obfuscated;

    /// <summary>Only use injections from these categories (empty = all).</summary>
    public ISet<string> Categories { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Only use injections having at least one of these tags (empty = all).</summary>
    public ISet<string> Tags { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Only use injections in these languages (empty = all).</summary>
    public ISet<string> Languages { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public InjectionOptions WithDifficulty(Difficulty difficulty)
    {
        Difficulty = difficulty;
        return this;
    }

    public InjectionOptions InCategory(params string[] categories)
    {
        Categories.UnionWith(categories);
        return this;
    }

    public InjectionOptions WithTag(params string[] tags)
    {
        Tags.UnionWith(tags);
        return this;
    }

    public InjectionOptions InLanguage(params string[] languages)
    {
        Languages.UnionWith(languages);
        return this;
    }

    internal bool Matches(InjectionDefinition d) =>
        (Categories.Count == 0 || Categories.Contains(d.Category))
        && (Languages.Count == 0 || Languages.Contains(d.Language))
        && (Tags.Count == 0 || d.Tags.Any(Tags.Contains));
}
