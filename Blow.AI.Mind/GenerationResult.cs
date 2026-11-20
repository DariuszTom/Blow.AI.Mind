using Blow.AI.Mind.Injections;

namespace Blow.AI.Mind;

/// <summary>Manifest of a generation run. It never contains passwords.</summary>
public sealed record GenerationResult(string Template, IReadOnlyList<GeneratedFile> Files);

/// <summary>A single generated workbook.</summary>
/// <param name="Id">Unique file identifier, also used in the file name. Use it to correlate a served file with later beacon hits.</param>
/// <param name="FileName">File name, e.g. <c>usersecrets-&lt;id&gt;.xlsx</c>.</param>
/// <param name="Path">Full path of the file, or <c>null</c> when written to a stream or memory.</param>
/// <param name="CreatedAt">UTC generation time.</param>
/// <param name="Rows">Number of user records.</param>
/// <param name="Encrypted">Whether the file is protected by a random, discarded open password.</param>
/// <param name="Injections">Planted injection metadata.</param>
public sealed record GeneratedFile(
    Guid Id,
    string FileName,
    string? Path,
    DateTimeOffset CreatedAt,
    int Rows,
    bool Encrypted,
    IReadOnlyList<InjectionRecord> Injections);

/// <summary>A workbook generated in memory, ready to be returned from an HTTP endpoint or stored elsewhere.</summary>
/// <param name="Content">The <c>.xlsx</c> (or encrypted container) bytes.</param>
/// <param name="Manifest">File metadata. Keep it on the server; never send it with the file.</param>
public sealed record GeneratedWorkbook(byte[] Content, GeneratedFile Manifest)
{
    /// <summary>MIME type of an Excel workbook (also used for encrypted files, which Excel opens with a password prompt).</summary>
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Shortcut for <see cref="GeneratedFile.FileName"/>.</summary>
    public string FileName => Manifest.FileName;
}

/// <summary>Where and how an injection was planted.</summary>
/// <param name="InjectionId">Definition identifier.</param>
/// <param name="Category">Definition category.</param>
/// <param name="Marker">Unique canary marker (<c>BAM-...</c>); its presence is an indicator, not proof of instruction following.</param>
/// <param name="Difficulty">Configured difficulty.</param>
/// <param name="Technique">Applied transformation.</param>
/// <param name="Placement">Type of location.</param>
/// <param name="Sheet">Worksheet name, or <c>null</c> for workbook-level placements (document properties, defined names).</param>
/// <param name="Locations">A1 cell references, custom property names or defined names containing the payload.</param>
/// <param name="BeaconUrl">Unique beacon URL planted via <c>{{beacon}}</c>, otherwise <c>null</c>.</param>
/// <param name="Command">Expanded code planted via <c>{{command}}</c>, otherwise <c>null</c>. The package does not execute it.</param>
public sealed record InjectionRecord(
    string InjectionId,
    string Category,
    string Marker,
    Difficulty Difficulty,
    ObfuscationTechnique Technique,
    InjectionPlacement Placement,
    string? Sheet,
    IReadOnlyList<string> Locations,
    Uri? BeaconUrl = null,
    string? Command = null);
