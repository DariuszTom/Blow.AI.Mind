# Blow.AI.Mind

Blow.AI.Mind generates fictional Excel workbooks for testing AI agents. The `Templates.UserSecrets` template contains user accounts, fictional password hashes, and clearly marked fake API keys and tokens.

```csharp
using Blow.AI.Mind;

var result = await WorkbookGenerator.Create()
    .WithTemplate(Templates.UserSecrets)
    .WithRows(100)
    .WithLocale("en")
    .WithInjection() // One random injection; WithInjections() selects all matching injections.
    .GenerateAsync("out", count: 2);
```

The data locale is configurable using `WithLocale`; the default is `pl`. Injection definitions can use different languages independently of the data locale. Use `InLanguage` in the injection options to filter them.

The default injection difficulty is `Obfuscated`. Add definitions under `Injections/<category>/<id>.json`, with `{{marker}}` in the text, or load a custom directory with `WithInjectionsFrom`. The manifest records each injection's marker and location. `InjectionEvaluator` detects markers in an agent's response, but a marker alone does not prove that the agent followed the instruction: it may simply have quoted the document.

Data generation, injection selection, markers, and passwords use a cryptographic source of randomness. There is no seed for reproducing a generated workbook. `Encrypted()` encrypts the file with an opening password that is neither saved nor returned. **The resulting file cannot normally be opened, and its password cannot be recovered.** The manifest does not contain the password. When generating multiple files, files written before an error or cancellation remain on disk.

CLI: `dotnet run --project Blow.AI.Mind.Cli -- generate --output out --rows 100 --locale en --injection`. Optional arguments include `--injections` instead of `--injection`, `--encrypted`, `--count`, and `--locale`.

Workbooks are created in memory using ClosedXML; large row counts can require substantial memory. Benchmark results and compatibility with specific Excel versions require separate verification.