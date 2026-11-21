# Contributing to Blow.AI.Mind

Thanks for your interest in improving Blow.AI.Mind! Bug reports, new fake-data generators, new injection scenarios, documentation fixes and tests are all welcome.

## Getting started

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download) (the exact feature band is pinned in `global.json`).
2. Fork and clone the repository, then run:

   ```shell
   dotnet restore Blow.AI.Mind.slnx
   dotnet build Blow.AI.Mind.slnx
   dotnet test Blow.AI.Mind.Tests
   ```

3. Try the CLI:

   ```shell
   dotnet run --project Blow.AI.Mind.Cli -- generate --output out --rows 100 --injection
   ```

## Project layout

| Project | Purpose |
|---|---|
| `Blow.AI.Mind` | The NuGet library: fake data, workbook templates, injections, encryption. |
| `Blow.AI.Mind.Cli` | `blow-ai-mind` .NET tool built on top of the library. |
| `Blow.AI.Mind.Tests` | NUnit + Shouldly tests. |
| `Blow.AI.Mind.Benchmarks` | BenchmarkDotNet benchmarks (`dotnet run -c Release --project Blow.AI.Mind.Benchmarks`). |

## Adding an injection scenario

Injection scenarios are plain JSON files in `Blow.AI.Mind/Injections/<category>/<id>.json`, embedded automatically at build time.

- Follow `Blow.AI.Mind/Injections/schema.json` and add `"$schema": "../schema.json"` for editor validation.
- `id` must be unique, lowercase, and match the file name.
- `text` must contain `{{marker}}`, `{{beacon}}` or `{{command}}`.
- Never include real secrets, real credentials, real URLs of third parties, or instructions that send data anywhere other than the library user's own `{{beacon}}`.
- Run `dotnet test` – `InjectionTests` validates every built-in definition.

## Coding guidelines

- Style is defined in `.editorconfig`; run `dotnet format Blow.AI.Mind.slnx` before committing.
- Public APIs need XML documentation comments.
- Use the cryptographic randomizer for anything random (data, markers, passwords). Do not add seeds.
- Never log, store or return generated workbook passwords.
- Fake secrets must keep a clearly fictional prefix (e.g. `FAKE-`, `bam_test_`).
- Add or update tests for behaviour changes.

## Pull requests

1. Create a branch from `master`.
2. Keep pull requests focused; describe *what* and *why*.
3. Make sure `dotnet build` and `dotnet test` pass – CI runs them on Ubuntu and Windows.
4. Update `README.md` when you change public behaviour or CLI options.

Releases are created by maintainers by pushing a `v*` tag; the version is computed by MinVer, so don't edit version numbers manually.

## Reporting security issues

Please don't open public issues for vulnerabilities – see [SECURITY.md](SECURITY.md).
