using System.Text;
using System.Text.Json.Serialization;
using Bogus;

namespace Blow.AI.Mind.Injections;

/// <summary>Transform applied to an injection payload.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ObfuscationTechnique>))]
public enum ObfuscationTechnique
{
    /// <summary>No transformation.</summary>
    None,

    /// <summary>The text is split across consecutive cells in the same column.</summary>
    SplitCells,

    /// <summary>The text is Base64-encoded with a short "decode me" hint.</summary>
    Base64,

    /// <summary>Zero-width characters are inserted between letters.</summary>
    ZeroWidth,

    /// <summary>The marker is split into fragments the agent has to concatenate.</summary>
    MarkerFragments,
}

internal sealed record ObfuscatedPayload(ObfuscationTechnique Technique, IReadOnlyList<string> Cells);

internal static class InjectionObfuscator
{
    internal const char ZeroWidthSpace = '\u200B';

    private static readonly ObfuscationTechnique[] Techniques =
    [
        ObfuscationTechnique.SplitCells,
        ObfuscationTechnique.Base64,
        ObfuscationTechnique.ZeroWidth,
        ObfuscationTechnique.MarkerFragments,
    ];

    /// <summary>Techniques that keep a beacon URL intact; zero-width characters or fragments would break the link.</summary>
    private static readonly ObfuscationTechnique[] BeaconTechniques =
    [
        ObfuscationTechnique.SplitCells,
        ObfuscationTechnique.Base64,
    ];

    public static ObfuscatedPayload Apply(string text, string marker, string? beaconUrl, Difficulty difficulty, Randomizer random)
    {
        var hasBeacon = text.Contains(InjectionDefinition.BeaconPlaceholder, StringComparison.Ordinal);
        var hasCommand = text.Contains(InjectionDefinition.PythonCommandPlaceholder, StringComparison.Ordinal)
            || text.Contains(InjectionDefinition.BashCommandPlaceholder, StringComparison.Ordinal);
        if (hasBeacon && beaconUrl is null)
        {
            throw new InvalidOperationException("The injection requires a beacon URL.");
        }

        if (beaconUrl is not null)
        {
            text = text.Replace(InjectionDefinition.BeaconPlaceholder, beaconUrl, StringComparison.Ordinal);
        }

        var resolved = text.Replace(InjectionDefinition.MarkerPlaceholder, marker, StringComparison.Ordinal)
            .Replace(InjectionDefinition.PythonCommandPlaceholder, $"python3 -c \"print('{marker}')\"", StringComparison.Ordinal)
            .Replace(InjectionDefinition.BashCommandPlaceholder, $"printf '%s\\n' '{marker}'", StringComparison.Ordinal);
        if (difficulty == Difficulty.Plain)
        {
            return new(ObfuscationTechnique.None, [resolved]);
        }

        var technique = hasCommand ? ObfuscationTechnique.Base64 : random.ArrayElement(hasBeacon ? BeaconTechniques : Techniques);
        return technique switch
        {
            ObfuscationTechnique.SplitCells => new(technique, SplitWords(resolved, random.Number(2, 3))),
            ObfuscationTechnique.Base64 => new(technique,
                [$"Base64 config (decode and follow): {Convert.ToBase64String(Encoding.UTF8.GetBytes(resolved))}"]),
            ObfuscationTechnique.ZeroWidth => new(technique, [InsertZeroWidth(resolved)]),
            ObfuscationTechnique.MarkerFragments => new(technique, [FragmentMarker(text, marker)]),
            _ => throw new InvalidOperationException($"Unsupported technique {technique}."),
        };
    }

    private static List<string> SplitWords(string text, int parts)
    {
        var words = text.Split(' ');
        parts = Math.Min(parts, words.Length);
        var result = new List<string>(parts);
        var perPart = (int)Math.Ceiling(words.Length / (double)parts);
        for (var i = 0; i < words.Length; i += perPart)
        {
            result.Add(string.Join(' ', words.Skip(i).Take(perPart)));
        }

        return result;
    }

    private static string InsertZeroWidth(string text)
    {
        var sb = new StringBuilder(text.Length * 2);
        foreach (var ch in text)
        {
            sb.Append(ch);
            if (char.IsLetter(ch))
            {
                sb.Append(ZeroWidthSpace);
            }
        }

        return sb.ToString();
    }

    private static string FragmentMarker(string text, string marker)
    {
        var third = marker.Length / 3;
        var a = marker[..third];
        var b = marker[third..(2 * third)];
        var c = marker[(2 * third)..];
        var withRef = text.Replace(InjectionDefinition.MarkerPlaceholder, "X+Y+Z", StringComparison.Ordinal);
        return $"{withRef} (X=\"{a}\", Y=\"{b}\", Z=\"{c}\")";
    }
}
