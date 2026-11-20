using Bogus;
using Blow.AI.Mind.Encryption;
using Blow.AI.Mind.Injections;
using Blow.AI.Mind.Templating;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blow.AI.Mind;

/// <summary>
/// Fluent entry point for generating fictional Excel workbooks.
/// </summary>
/// <example>
/// <code>
/// var result = await WorkbookGenerator.Create()
///     .WithTemplate(Templates.UserSecrets)
///     .WithRows(200)
///     .WithInjection()
///     .Encrypted()
///     .GenerateAsync("out", count: 10);
/// </code>
/// </example>
public sealed class WorkbookGenerator
{
    public const int MaxRows = 1_000_000;

    private enum InjectionMode
    {
        None,
        Single,
        All,
    }

    private WorkbookTemplate _template = Templates.UserSecrets;
    private int _rows = 100;
    private string _locale = "en";
    private InjectionMode _injectionMode;
    private InjectionOptions _injectionOptions = new();
    private IReadOnlyList<InjectionDefinition>? _customInjections;
    private bool _encrypted;
    private IReadOnlyList<string> _emailDomains = [];
    private ILogger _logger = NullLogger.Instance;

    private WorkbookGenerator()
    {
    }

    /// <summary>Test hook: receives the random password of every encrypted file. Never exposed publicly.</summary>
    internal Action<GeneratedFile, string>? PasswordObserver { get; set; }

    public static WorkbookGenerator Create() => new();

    public WorkbookGenerator WithTemplate(WorkbookTemplate template)
    {
        _template = template ?? throw new ArgumentNullException(nameof(template));
        return this;
    }

    public WorkbookGenerator WithRows(int rows)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rows, MaxRows);
        _rows = rows;
        return this;
    }

    /// <summary>Bogus locale, e.g. <c>pl</c>, <c>en</c>, <c>de</c>. Defaults to <c>en</c>.</summary>
    public WorkbookGenerator WithLocale(string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        if (!Database.LocaleResourceExists(locale))
        {
            throw new ArgumentException($"Unknown Bogus locale '{locale}'.", nameof(locale));
        }

        _locale = locale;
        return this;
    }

    /// <summary>
    /// Email domains to pick from, e.g. <c>contoso.com</c>. By default Bogus uses the locale's
    /// free-email providers (e.g. <c>gmail.com</c>, <c>yahoo.com</c>).
    /// </summary>
    public WorkbookGenerator WithEmailDomains(params string[] domains)
    {
        ArgumentNullException.ThrowIfNull(domains);
        if (domains.Length == 0)
        {
            throw new ArgumentException("At least one email domain is required.", nameof(domains));
        }

        var normalized = new List<string>(domains.Length);
        foreach (var domain in domains)
        {
            var value = domain?.Trim().TrimStart('@').ToLowerInvariant();
            if (string.IsNullOrEmpty(value) || !value.Contains('.') || Uri.CheckHostName(value) != UriHostNameType.Dns)
            {
                throw new ArgumentException($"Invalid email domain '{domain}'.", nameof(domains));
            }

            normalized.Add(value);
        }

        _emailDomains = normalized.Distinct().ToList();
        return this;
    }

    /// <summary>
    /// Plants one randomly chosen injection per file in one randomly chosen placement from
    /// <see cref="InjectionOptions.InPlacement"/> (default: <see cref="InjectionPlacement.Cell"/>; default difficulty: <see cref="Difficulty.Obfuscated"/>).
    /// </summary>
    public WorkbookGenerator WithInjection(Action<InjectionOptions>? configure = null) => SetInjections(InjectionMode.Single, configure);

    /// <summary>
    /// Plants every matching injection in every configured placement of every file
    /// (default placement: <see cref="InjectionPlacement.Cell"/>; default difficulty: <see cref="Difficulty.Obfuscated"/>).
    /// </summary>
    public WorkbookGenerator WithInjections(Action<InjectionOptions>? configure = null) => SetInjections(InjectionMode.All, configure);

    /// <summary>Uses injection definitions from a directory instead of the built-in library.</summary>
    public WorkbookGenerator WithInjectionsFrom(string directory)
    {
        _customInjections = InjectionLibrary.LoadFromDirectory(directory);
        return this;
    }

    /// <summary>Protects files with a random, cryptographically strong open password that is immediately discarded.</summary>
    public WorkbookGenerator Encrypted(bool encrypted = true)
    {
        _encrypted = encrypted;
        return this;
    }

    public WorkbookGenerator WithLogger(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        return this;
    }

    /// <summary>Generates <paramref name="count"/> files into <paramref name="outputDirectory"/>. Existing files are never overwritten.</summary>
    public async Task<GenerationResult> GenerateAsync(string outputDirectory, int count = 1, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        var injections = ResolveInjections();

        Directory.CreateDirectory(outputDirectory);
        var paths = Enumerable.Range(0, count)
            .Select(_ => Path.GetFullPath(Path.Combine(outputDirectory, $"{_template.Name.ToLowerInvariant()}-{Guid.NewGuid():N}.xlsx")))
            .ToList();
        var existing = paths.FirstOrDefault(File.Exists);
        if (existing is not null)
        {
            throw new IOException($"File '{existing}' already exists.");
        }

        var files = new List<GeneratedFile>(count);
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (bytes, file) = Build(paths[i], injections);
            var temp = paths[i] + $".{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllBytesAsync(temp, bytes, cancellationToken).ConfigureAwait(false);
                File.Move(temp, paths[i], overwrite: false);
            }
            finally
            {
                File.Delete(temp);
            }

            files.Add(file);
        }

        return new GenerationResult(_template.Name, files);
    }

    /// <summary>Generates a single file into <paramref name="output"/>.</summary>
    public async Task<GeneratedFile> GenerateAsync(Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        cancellationToken.ThrowIfCancellationRequested();
        var (bytes, file) = Build(null, ResolveInjections());
        await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        return file;
    }

    private WorkbookGenerator SetInjections(InjectionMode mode, Action<InjectionOptions>? configure)
    {
        _injectionMode = mode;
        _injectionOptions = new InjectionOptions();
        configure?.Invoke(_injectionOptions);
        if (!Enum.IsDefined(_injectionOptions.Difficulty))
        {
            throw new ArgumentOutOfRangeException(nameof(configure), "Unsupported injection difficulty.");
        }
        return this;
    }

    private IReadOnlyList<InjectionDefinition> ResolveInjections()
    {
        if (_injectionMode == InjectionMode.None)
        {
            return [];
        }

        var placements = _injectionOptions.EffectivePlacements;
        var matching = (_customInjections ?? InjectionLibrary.BuiltIn)
            .Where(d => d.Placements.Any(placements.Contains) && _injectionOptions.Matches(d))
            .ToList();
        if (matching.Count == 0)
        {
            throw new InvalidOperationException("No injection definitions match the configured filters.");
        }

        return matching;
    }

    private (byte[] Bytes, GeneratedFile File) Build(string? path, IReadOnlyList<InjectionDefinition> injections)
    {
        var random = new CryptographicRandomizer();
        var faker = new Faker(_locale) { Random = random };
        using var workbook = new XLWorkbook();
        var target = _template.Build(workbook, new TemplateContext(faker, _rows, _emailDomains));

        var records = PlantInjections(workbook, target, injections, random);

        byte[] bytes;
        using (var plain = new MemoryStream())
        {
            workbook.SaveAs(plain);
            bytes = plain.ToArray();
        }

        var file = new GeneratedFile(path, _rows, _encrypted, records);
        if (_encrypted)
        {
            var password = PasswordGenerator.Create();
            using var encrypted = new MemoryStream();
            AgileEncryptor.Encrypt(bytes, encrypted, password);
            Array.Clear(bytes);
            bytes = encrypted.ToArray();
            PasswordObserver?.Invoke(file, password);
        }

        _logger.LogInformation("Generated {Template} workbook (rows {Rows}, injections {Injections}, encrypted {Encrypted}) {Path}",
            _template.Name, _rows, records.Count, _encrypted, path ?? "<stream>");
        return (bytes, file);
    }

    private List<InjectionRecord> PlantInjections(XLWorkbook workbook, InjectionTarget target, IReadOnlyList<InjectionDefinition> injections, Randomizer random)
    {
        if (injections.Count == 0)
        {
            return [];
        }

        var placements = _injectionOptions.EffectivePlacements;
        IEnumerable<(InjectionDefinition Definition, InjectionPlacement Placement)> selected;
        if (_injectionMode == InjectionMode.Single)
        {
            var definition = random.ListItem(injections.ToList());
            selected = [(definition, random.ListItem(placements.Where(definition.Placements.Contains).ToList()))];
        }
        else
        {
            selected = injections.SelectMany(d => placements.Where(d.Placements.Contains).Select(p => (d, p)));
        }

        var planned = selected.Select(s =>
        {
            var marker = $"BAM-{Guid.NewGuid():N}".ToUpperInvariant();
            return new PlannedInjection(s.Definition, s.Placement, marker,
                InjectionObfuscator.Apply(s.Definition.Text, marker, _injectionOptions.Difficulty, random));
        }).ToList();

        return new InjectionPlanter(workbook, target, random).Plant(planned, _injectionOptions.Difficulty);
    }

}
