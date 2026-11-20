using System.ComponentModel;
using Blow.AI.Mind.Injections;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class GenerateSettings : CommandSettings
{
    [CommandOption("-o|--output <DIRECTORY>")]
    [DefaultValue("out")]
    public string Output { get; init; } = "out";

    [CommandOption("--rows <COUNT>")]
    [DefaultValue(100)]
    public int Rows { get; init; } = 100;

    [CommandOption("--count <COUNT>")]
    [DefaultValue(1)]
    public int Count { get; init; } = 1;

    [CommandOption("--locale <LOCALE>")]
    [DefaultValue("en")]
    public string Locale { get; init; } = "en";

    [CommandOption("--email-domain <DOMAIN>")]
    [Description("Email domain to use (repeatable). Defaults to the locale's free-email providers.")]
    public string[] EmailDomains { get; init; } = [];

    [CommandOption("--injection")]
    public bool Injection { get; init; }

    [CommandOption("--injections")]
    public bool Injections { get; init; }

    [CommandOption("--placement <PLACEMENT>")]
    [Description("Injection placement (repeatable): Cell, Comment, HiddenCell, HiddenSheet, DocumentProperties, DefinedName or All. Defaults to Cell.")]
    public string[] Placements { get; init; } = [];

    [CommandOption("--encrypted")]
    public bool Encrypted { get; init; }

    [CommandOption("--beacon <URL>")]
    [Description("Base URL on a host you control for {{beacon}} injections, e.g. https://files.example.com/b.")]
    public string? Beacon { get; init; }

    [CommandOption("--command <CODE>")]
    [Description("Code planted by {{command}} injections. Must contain {{marker}} or {{beacon}}. It is not executed.")]
    public string? Command { get; init; }

    public override ValidationResult Validate()
    {
        if (Injection && Injections)
        {
            return ValidationResult.Error("Choose either --injection or --injections.");
        }

        if (Beacon is not null && !Uri.TryCreate(Beacon, UriKind.Absolute, out _))
        {
            return ValidationResult.Error($"Invalid beacon URL '{Beacon}'.");
        }

        if (Command is not null && (string.IsNullOrWhiteSpace(Command)
            || Command.Contains("{{command}}", StringComparison.Ordinal)
            || (!Command.Contains("{{marker}}", StringComparison.Ordinal) && !Command.Contains("{{beacon}}", StringComparison.Ordinal))))
        {
            return ValidationResult.Error("Command must contain {{marker}} or {{beacon}}, and must not contain {{command}}.");
        }

        var invalid = Placements.FirstOrDefault(p => !string.Equals(p, "All", StringComparison.OrdinalIgnoreCase)
            && !Enum.TryParse<InjectionPlacement>(p, ignoreCase: true, out _));
        return invalid is null ? ValidationResult.Success() : ValidationResult.Error($"Unknown placement '{invalid}'.");
    }

    public void Configure(InjectionOptions options)
    {
        foreach (var placement in Placements)
        {
            if (string.Equals(placement, "All", StringComparison.OrdinalIgnoreCase))
            {
                options.InAllPlacements();
            }
            else
            {
                options.InPlacement(Enum.Parse<InjectionPlacement>(placement, ignoreCase: true));
            }
        }
    }
}
