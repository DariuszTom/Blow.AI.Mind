
namespace Blow.AI.Mind.Injections;

/// <summary>
/// Loads and validates injection definitions from the embedded library or from a directory of JSON files.
/// </summary>
public static class InjectionLibrary
{
    private const string ResourcePrefix = "Blow.AI.Mind.Injections.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        RespectRequiredConstructorParameters = true,
    };

    private static readonly Lazy<IReadOnlyList<InjectionDefinition>> BuiltInLazy = new(LoadBuiltIn);

    /// <summary>Injections shipped with the package, ordered by <see cref="InjectionDefinition.Id"/>.</summary>
    public static IReadOnlyList<InjectionDefinition> BuiltIn => BuiltInLazy.Value;

    /// <summary>Loads all <c>*.json</c> files (recursively, excluding <c>schema.json</c>) from a directory.</summary>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    /// <exception cref="InvalidDataException">A file is not a valid injection definition.</exception>
    public static IReadOnlyList<InjectionDefinition> LoadFromDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Injection directory '{path}' does not exist.");
        }

        var result = new List<InjectionDefinition>();
        foreach (var file in Directory.EnumerateFiles(path, "*.json", SearchOption.AllDirectories)
                     .Where(f => !string.Equals(Path.GetFileName(f), "schema.json", StringComparison.OrdinalIgnoreCase))
                     .Order(StringComparer.Ordinal))
        {
            using var stream = File.OpenRead(file);
            result.Add(Parse(stream, file));
        }

        EnsureUniqueIds(result);
        return result;
    }

    /// <summary>Parses and validates a single injection definition.</summary>
    /// <exception cref="InvalidDataException">The content is not a valid injection definition.</exception>
    public static InjectionDefinition Parse(Stream json, string sourceName)
    {
        InjectionDefinition? definition;
        try
        {
            definition = JsonSerializer.Deserialize<InjectionDefinition>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Injection '{sourceName}' is not valid JSON: {ex.Message}", ex);
        }

        if (definition is null)
        {
            throw new InvalidDataException($"Injection '{sourceName}' is empty.");
        }

        Validate(definition, sourceName);
        return definition;
    }

    internal static void Validate(InjectionDefinition definition, string sourceName)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            errors.Add("'id' is required");
        }

        if (string.IsNullOrWhiteSpace(definition.Category))
        {
            errors.Add("'category' is required");
        }

        if (string.IsNullOrWhiteSpace(definition.Text)
            || (!definition.Text.Contains(InjectionDefinition.MarkerPlaceholder, StringComparison.Ordinal)
                && !definition.RequiresBeacon
                && !definition.RequiresCommand))
        {
            errors.Add($"'text' must contain {InjectionDefinition.MarkerPlaceholder}, {InjectionDefinition.BeaconPlaceholder}, or {InjectionDefinition.CommandPlaceholder}");
        }

        if (definition.Placements is null || definition.Placements.Count == 0)
        {
            errors.Add("'placements' must contain at least one value");
        }
        else if (definition.Placements.Any(p => !Enum.IsDefined(p)))
        {
            errors.Add("'placements' contains an unsupported value");
        }

        if (string.IsNullOrWhiteSpace(definition.Language))
        {
            errors.Add("'language' must not be empty");
        }

        if (definition.Tags is null || definition.Tags.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("'tags' must contain only non-empty strings");
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException($"Injection '{sourceName}' is invalid: {string.Join("; ", errors)}.");
        }
    }

    internal static void EnsureUniqueIds(IEnumerable<InjectionDefinition> definitions)
    {
        var duplicates = definitions.GroupBy(d => d.Id, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
        {
            throw new InvalidDataException($"Duplicate injection ids: {string.Join(", ", duplicates)}.");
        }
    }

    private static IReadOnlyList<InjectionDefinition> LoadBuiltIn()
    {
        var assembly = typeof(InjectionLibrary).Assembly;
        var result = new List<InjectionDefinition>();
        foreach (var name in assembly.GetManifestResourceNames()
                     .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                     .Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            result.Add(Parse(stream, name));
        }

        EnsureUniqueIds(result);
        return [.. result.OrderBy(d => d.Id, StringComparer.Ordinal)];
    }
}
