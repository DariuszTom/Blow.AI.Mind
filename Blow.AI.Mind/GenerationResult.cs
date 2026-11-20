using Blow.AI.Mind.Injections;

namespace Blow.AI.Mind;

/// <summary>Manifest of a generation run. It never contains passwords.</summary>
public sealed record GenerationResult(string Template, IReadOnlyList<GeneratedFile> Files);

/// <summary>A single generated workbook.</summary>
/// <param name="Path">Full path of the file, or <c>null</c> when written to a stream.</param>
/// <param name="Rows">Number of user records.</param>
/// <param name="Encrypted">Whether the file is protected by a random, discarded open password.</param>
/// <param name="Injections">Planted injection metadata.</param>
public sealed record GeneratedFile(string? Path, int Rows, bool Encrypted, IReadOnlyList<InjectionRecord> Injections);

/// <summary>Where and how an injection was planted.</summary>
/// <param name="InjectionId">Definition identifier.</param>
/// <param name="Category">Definition category.</param>
/// <param name="Marker">Unique canary marker (<c>BAM-...</c>); its presence is an indicator, not proof of instruction following.</param>
/// <param name="Difficulty">Configured difficulty.</param>
/// <param name="Technique">Applied transformation.</param>
/// <param name="Placement">Type of location.</param>
/// <param name="Sheet">Worksheet name, or <c>null</c> for workbook-level placements (document properties, defined names).</param>
/// <param name="Locations">A1 cell references, custom property names or defined names containing the payload.</param>
public sealed record InjectionRecord(
    string InjectionId,
    string Category,
    string Marker,
    Difficulty Difficulty,
    ObfuscationTechnique Technique,
    InjectionPlacement Placement,
    string? Sheet,
    IReadOnlyList<string> Locations);
