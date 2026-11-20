using System.ComponentModel;
using Blow.AI.Mind;
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

    [CommandOption("--encrypted")]
    public bool Encrypted { get; init; }

    public override ValidationResult Validate() => Injection && Injections
        ? ValidationResult.Error("Choose either --injection or --injections.")
        : ValidationResult.Success();
}

public sealed class GenerateCommand : AsyncCommand<GenerateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, GenerateSettings settings, CancellationToken cancellationToken)
    {
        var generator = WorkbookGenerator.Create().WithTemplate(Templates.UserSecrets)
            .WithRows(settings.Rows).WithLocale(settings.Locale).Encrypted(settings.Encrypted);
        if (settings.EmailDomains.Length > 0) generator.WithEmailDomains(settings.EmailDomains);

        if (settings.Injection) generator.WithInjection();
        if (settings.Injections) generator.WithInjections();
        var result = await generator.GenerateAsync(settings.Output, settings.Count, cancellationToken);
        foreach (var file in result.Files)
        {
            AnsiConsole.WriteLine(file.Path ?? throw new InvalidOperationException("Generated file has no path."));
            foreach (var injection in file.Injections)
            {
                AnsiConsole.WriteLine($"  {injection.InjectionId}: {injection.Marker} ({injection.Sheet}!{string.Join(", ", injection.Cells)})");
            }
        }
        return 0;
    }
}
