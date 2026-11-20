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

    /// <summary>Workbook locations to use. Empty means <see cref="InjectionPlacement.Cell"/> only.</summary>
    public ISet<InjectionPlacement> Placements { get; } = new HashSet<InjectionPlacement>();

    /// <summary>Adds workbook locations; several can be combined, e.g. <c>InPlacement(Comment, HiddenSheet)</c>.</summary>
    public InjectionOptions InPlacement(params InjectionPlacement[] placements)
    {
        ArgumentNullException.ThrowIfNull(placements);
        foreach (var placement in placements)
        {
            if (!Enum.IsDefined(placement))
            {
                throw new ArgumentOutOfRangeException(nameof(placements), placement, "Unsupported injection placement.");
            }

            Placements.Add(placement);
        }

        return this;
    }

    /// <summary>Uses every supported workbook location.</summary>
    public InjectionOptions InAllPlacements() => InPlacement(Enum.GetValues<InjectionPlacement>());

    internal IReadOnlyCollection<InjectionPlacement> EffectivePlacements =>
        Placements.Count == 0 ? [InjectionPlacement.Cell] : Placements.Order().ToList();

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
