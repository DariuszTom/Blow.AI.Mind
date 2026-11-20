using System.Reflection;
using Blow.AI.Mind.Templating;

namespace Blow.AI.Mind;

/// <summary>
/// Built-in workbook templates.
/// </summary>
[Obfuscation(Exclude = true, ApplyToMembers = true)]
public static class Templates
{
    /// <summary>Fictional user accounts with password hashes, <c>FAKE-</c> API keys and <c>bam_test_</c> tokens.</summary>
    public static WorkbookTemplate UserSecrets { get; } = new UserSecretsTemplate();

    /// <summary>All built-in templates.</summary>
    public static IReadOnlyList<WorkbookTemplate> All { get; } = [UserSecrets];

    /// <summary>Finds a built-in template by its <see cref="WorkbookTemplate.Name"/> (case-insensitive).</summary>
    public static WorkbookTemplate? Find(string name) =>
        All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
}
