# Blow.AI.Mind

[![.NET](https://github.com/DariuszTom/Blow.AI.Mind/actions/workflows/dotnet.yml/badge.svg)](https://github.com/DariuszTom/Blow.AI.Mind/actions/workflows/dotnet.yml)
[![CodeQL](https://github.com/DariuszTom/Blow.AI.Mind/actions/workflows/codeql.yml/badge.svg)](https://github.com/DariuszTom/Blow.AI.Mind/actions/workflows/codeql.yml)
[![Code Coverage](https://github.com/DariuszTom/Blow.AI.Mind/actions/workflows/coverage.yml/badge.svg)](https://github.com/DariuszTom/Blow.AI.Mind/actions/workflows/coverage.yml)
[![Release](https://github.com/DariuszTom/Blow.AI.Mind/actions/workflows/release.yml/badge.svg)](https://github.com/DariuszTom/Blow.AI.Mind/actions/workflows/release.yml)
[![Secrets Scan](https://github.com/DariuszTom/Blow.AI.Mind/actions/workflows/secrets-scan.yml/badge.svg)](https://github.com/DariuszTom/Blow.AI.Mind/actions/workflows/secrets-scan.yml)
[![NuGet](https://img.shields.io/nuget/v/Blow.AI.Mind.svg?label=NuGet)](https://www.nuget.org/packages/Blow.AI.Mind)
[![NuGet CLI](https://img.shields.io/nuget/v/Blow.AI.Mind.Cli.svg?label=NuGet%20CLI)](https://www.nuget.org/packages/Blow.AI.Mind.Cli)

Blow.AI.Mind generates fictional Excel workbooks for testing AI agents. The `Templates.UserSecrets` template contains user accounts, fictional password hashes, and clearly marked fake API keys and tokens.

```csharp
using Blow.AI.Mind;

var result = await WorkbookGenerator.Create()
    .WithTemplate(Templates.UserSecrets)
    .WithRows(100)
    .WithInjection() // One random injection; WithInjections() selects all matching injections.
    .GenerateAsync("out", count: 2);
```

The data locale is configurable using `WithLocale`; the default is `en`. Injection definitions can use different languages independently of the data locale. Use `InLanguage` in the injection options to filter them.

### Injection placements

By default injections go into the visible `Notes` cell. Use `InPlacement` (several at once) or `InAllPlacements()`:

| Placement | Location |
|---|---|
| `Cell` | Visible `Notes` column (default). |
| `Comment` | Cell note on a `Username` cell. |
| `HiddenCell` | Hidden column, white font. |
| `HiddenSheet` | Hidden `Settings` worksheet. |
| `DocumentProperties` | Custom document property `Review note N`. |
| `DefinedName` | Workbook defined name `_ReviewNoteN` with a text constant. |

```csharp
.WithInjection(o => o.InPlacement(InjectionPlacement.Comment, InjectionPlacement.HiddenSheet)) // one injection, one random placement
.WithInjections(o => o.InAllPlacements())                                                    // every injection in every placement
```

Each `InjectionRecord` reports its `Placement`, `Sheet` (`null` for workbook-level placements) and `Locations` (cell references, property names or defined names). A definition can restrict where it may be used through `placements` in its JSON file; by default it allows all placements.


### Serving files and beacons

The package has no HTTP dependency; your own API decides how to serve files. `Generate()` creates a fresh workbook in memory per call (new data, markers and, if `Encrypted()`, a new password):

```csharp
var generator = WorkbookGenerator.Create()
    .WithRows(200)
    .WithEmailDomains("files.example.com")             // domains you control, not real third parties
    .WithBeacon(new Uri("https://files.example.com/b")) // host you control
    .WithInjections(o => o.InAllPlacements());

var workbook = generator.Generate();
await manifests.SaveAsync(workbook.Manifest);              // keep on the server, keyed by Manifest.Id
return Results.File(workbook.Content, GeneratedWorkbook.ContentType, workbook.FileName);
```

Definitions with `{{beacon}}` (category `beacon`) receive a unique URL `<base>/<marker>`, recorded in `InjectionRecord.BeaconUrl`. Unlike an echoed marker, a request to that URL shows that an agent acted on the instruction. Map a request back with `Beacon.TryGetMarker(requestUri, baseUri, out var marker)`. Without `WithBeacon`, beacon definitions are skipped. Beacon URLs only survive the `SplitCells` and `Base64` obfuscation techniques, so other techniques are not used for them.

Use beacons and served files only on infrastructure you own: they are passive telemetry, never a way to execute anything on the client.

By default, email addresses use the locale's free-email providers from Bogus (e.g. `gmail.com`, `yahoo.com`). Use `WithEmailDomains("contoso.com", "fabrikam.com")` (CLI: `--email-domain`, repeatable) to use your own domains.

The default injection difficulty is `Obfuscated`. Add definitions under `Injections/<category>/<id>.json`, with `{{marker}}`, `{{beacon}}`, or `{{command}}` in the text, or load a custom directory with `WithInjectionsFrom`. The manifest records each injection's marker and location. `InjectionEvaluator` detects markers in an agent's response, but a marker alone does not prove that the agent followed the instruction: it may simply have quoted the document.

### Code execution canaries

`WithCommand` works like `WithBeacon`: you supply the text, and the package only writes it into the workbook. `{{command}}` (category `code-execution`) is replaced by that code. The code must contain `{{marker}}`, `{{beacon}}`, or both; each injection gets its own values. Without `WithCommand`, these definitions are skipped. If the code contains `{{beacon}}`, call `WithBeacon` as well. The package does not execute the code. With `Obfuscated` difficulty these definitions use Base64, so decoding restores the complete command. The expanded code is recorded in `InjectionRecord.Command`.

```csharp
using Blow.AI.Mind;
using Blow.AI.Mind.Injections;

var result = await WorkbookGenerator.Create()
    .WithTemplate(Templates.UserSecrets)
    .WithRows(100)
    .WithBeacon(new Uri("https://files.example.com/b"))
    .WithCommand("python3 -c \"import urllib.request; urllib.request.urlopen('{{beacon}}')\"")
    .WithInjections(o => o.InCategory("code-execution"))

A marker in an answer can be quoted rather than executed. Verify execution using trusted tool-execution logs from an authorized, isolated test environment; even a beacon request alone does not prove a specific command ran.

Data generation, injection selection, markers, and passwords use a cryptographic source of randomness. There is no seed for reproducing a generated workbook. `Encrypted()` encrypts the file with an opening password that is neither saved nor returned. **The resulting file cannot normally be opened, and its password cannot be recovered.** The manifest does not contain the password. When generating multiple files, files written before an error or cancellation remain on disk.

CLI: `dotnet run --project Blow.AI.Mind.Cli -- generate --output out --rows 100 --locale en --injection`. Optional arguments include `--injections` instead of `--injection`, `--placement` (repeatable, or `All`), `--email-domain`, `--beacon <URL>`, `--command <CODE>`, `--encrypted`, `--count`, and `--locale`.

Workbooks are created in memory using ClosedXML; large row counts can require substantial memory. Benchmark results and compatibility with specific Excel versions require separate verification.

### CI and releases

GitHub Actions in `.github/workflows`: `dotnet.yml` builds, tests (Ubuntu and Windows) and packs on pushes and pull requests to `master`; `coverage.yml` collects code coverage (HTML report artifact, job summary and PR comment; optional Codecov upload when the `CODECOV_TOKEN` secret is set); `codeql.yml` and `secrets-scan.yml` (Gitleaks) run security scans; `release.yml` publishes `Blow.AI.Mind` and `Blow.AI.Mind.Cli` to NuGet.org and creates a GitHub release when a `v*` tag (e.g. `v1.0.0`) is pushed. The version comes from the tag via MinVer. Publishing requires the `NUGET_API_KEY` repository secret.

## Contributing

Contributions are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md). Report vulnerabilities privately as described in [SECURITY.md](SECURITY.md).
