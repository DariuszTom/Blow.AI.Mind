using Blow.AI.Mind;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class GenerateCommand : AsyncCommand<GenerateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, GenerateSettings settings, CancellationToken cancellationToken)
    {
        var generator = WorkbookGenerator.Create().WithTemplate(Templates.UserSecrets)
            .WithRows(settings.Rows).WithLocale(settings.Locale).Encrypted(settings.Encrypted);
        if (settings.EmailDomains.Length > 0) generator.WithEmailDomains(settings.EmailDomains);
        if (settings.Beacon is not null) generator.WithBeacon(new Uri(settings.Beacon));

        if (settings.Injection) generator.WithInjection(settings.Configure);
        if (settings.Injections) generator.WithInjections(settings.Configure);
        var result = await generator.GenerateAsync(settings.Output, settings.Count, cancellationToken);
        foreach (var file in result.Files)
        {
            AnsiConsole.WriteLine(file.Path ?? throw new InvalidOperationException("Generated file has no path."));
            foreach (var injection in file.Injections)
            {
                var where = injection.Sheet is null ? string.Join(", ", injection.Locations) : $"{injection.Sheet}!{string.Join(", ", injection.Locations)}";
                var beacon = injection.BeaconUrl is null ? string.Empty : $" -> {injection.BeaconUrl}";
                AnsiConsole.WriteLine($"  {injection.InjectionId} [{injection.Placement}]: {injection.Marker} ({where}){beacon}");
            }
        }
        return 0;
    }
}
