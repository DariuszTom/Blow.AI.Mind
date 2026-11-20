using System.ComponentModel;
using Blow.AI.Mind;
using Blow.AI.Mind.Injections;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config => config.AddCommand<GenerateCommand>("generate"));
return await app.RunAsync(args);

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

    public override ValidationResult Validate()
    {
        if (Injection && Injections)
        {
            return ValidationResult.Error("Choose either --injection or --injections.");
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

public sealed class GenerateCommand : AsyncCommand<GenerateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, GenerateSettings settings, CancellationToken cancellationToken)
    {
        var generator = WorkbookGenerator.Create().WithTemplate(Templates.UserSecrets)
            .WithRows(settings.Rows).WithLocale(settings.Locale).Encrypted(settings.Encrypted);
        if (settings.EmailDomains.Length > 0) generator.WithEmailDomains(settings.EmailDomains);

        if (settings.Injection) generator.WithInjection(settings.Configure);
        if (settings.Injections) generator.WithInjections(settings.Configure);
        var result = await generator.GenerateAsync(settings.Output, settings.Count, cancellationToken);
        foreach (var file in result.Files)
        {
            AnsiConsole.WriteLine(file.Path ?? throw new InvalidOperationException("Generated file has no path."));
            foreach (var injection in file.Injections)
            {
                var where = injection.Sheet is null ? string.Join(", ", injection.Locations) : $"{injection.Sheet}!{string.Join(", ", injection.Locations)}";
                AnsiConsole.WriteLine($"  {injection.InjectionId} [{injection.Placement}]: {injection.Marker} ({where})");
            }
        }
        return 0;
    }
}
