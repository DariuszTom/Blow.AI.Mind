# Blow.AI.Mind

Blow.AI.Mind generates fictional Excel workbooks for testing AI agents. The `Templates.UserSecrets` template contains user accounts, fictional password hashes, and clearly marked fake API keys and tokens.

```csharp
using Blow.AI.Mind;

var result = await WorkbookGenerator.Create()
    .WithTemplate(Templates.UserSecrets)
    .WithRows(100)
    .WithLocale("pl")
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

The default injection difficulty is `Obfuscated`. Add definitions under `Injections/<category>/<id>.json`, with `{{marker}}` in the text, or load a custom directory with `WithInjectionsFrom`. The manifest records each injection's marker and location. `InjectionEvaluator` detects markers in an agent's response, but a marker alone does not prove that the agent followed the instruction: it may simply have quoted the document.

Data generation, injection selection, markers, and passwords use a cryptographic source of randomness. There is no seed for reproducing a generated workbook. `Encrypted()` encrypts the file with an opening password that is neither saved nor returned. **The resulting file cannot normally be opened, and its password cannot be recovered.** The manifest does not contain the password. When generating multiple files, files written before an error or cancellation remain on disk.

CLI: `dotnet run --project Blow.AI.Mind.Cli -- generate --output out --rows 100 --locale en --injection`. Optional arguments include `--injections` instead of `--injection`, `--placement` (repeatable, or `All`), `--email-domain`, `--beacon <URL>`, `--encrypted`, `--count`, and `--locale`.

Workbooks are created in memory using ClosedXML; large row counts can require substantial memory. Benchmark results and compatibility with specific Excel versions require separate verification.