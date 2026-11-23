using Blow.AI.Mind.Injections;
using Blow.AI.Mind.Templating;
using System.Text;

namespace Blow.AI.Mind.Tests;

public class InjectionTests
{
    [Test]
    public void Built_in_library_is_valid_and_non_empty()
    {
        InjectionLibrary.BuiltIn.Count.ShouldBeGreaterThanOrEqualTo(3);
        InjectionLibrary.BuiltIn.ShouldAllBe(d => d.Text.Contains(InjectionDefinition.MarkerPlaceholder) || d.RequiresBeacon || d.RequiresCommand);
        InjectionLibrary.BuiltIn.Select(d => d.Id).ShouldBeUnique();
    }

    [TestCase("self-harm-instructions")]
    [TestCase("violence-instructions")]
    [TestCase("phishing-content")]
    [TestCase("private-information-disclosure")]
    [TestCase("hate-content-pressure")]
    public void Policy_resilience_scenarios_are_available(string id)
    {
        var definition = InjectionLibrary.BuiltIn.Single(d => d.Id == id);
        definition.Category.ShouldBe("policy-resilience");
        definition.Text.ShouldContain(InjectionDefinition.MarkerPlaceholder);
    }

    [Test]
    public async Task WithInjection_plants_exactly_one_obfuscated_injection_by_default()
    {
        var (bytes, file) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithRows(40).WithInjection());

        var injection = file.Injections.ShouldHaveSingleItem();
        injection.Difficulty.ShouldBe(Difficulty.Obfuscated);
        injection.Technique.ShouldNotBe(ObfuscationTechnique.None);
        injection.Marker.ShouldStartWith("BAM-");
        injection.Sheet.ShouldBe(UserSecretsTemplate.UsersSheet);
        injection.Placement.ShouldBe(InjectionPlacement.Cell);
        injection.Locations.ShouldAllBe(c => c.StartsWith('M'));

        var users = TestHelpers.ReadExcel(bytes).Tables[UserSecretsTemplate.UsersSheet]!;
        var payload = string.Join(" ", injection.Locations.Select(cell => users.Rows[int.Parse(cell[1..]) - 2][UserSecretsTemplate.NotesColumn - 1]));
        ContainsMarker(payload, injection).ShouldBeTrue();
    }

    [Test]
    public async Task WithInjections_plants_all_matching_injections()
    {
        var (_, file) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithRows(80).WithInjections());

        file.Injections.Select(i => i.InjectionId).Order()
            .ShouldBe(InjectionLibrary.BuiltIn.Where(d => !d.RequiresBeacon && !d.RequiresCommand).Select(d => d.Id).Order());
        file.Injections.ShouldAllBe(i => i.BeaconUrl == null);
        file.Injections.Select(i => i.Marker).ShouldBeUnique();
        file.Injections.SelectMany(i => i.Locations).ShouldBeUnique();
    }

    [Test]
    public async Task Plain_difficulty_writes_marker_verbatim()
    {
        var (bytes, file) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithRows(InjectionLibrary.BuiltIn.Count)
            .WithInjections(o => o.WithDifficulty(Difficulty.Plain)));

        var notes = TestHelpers.CellsText(TestHelpers.ReadExcel(bytes).Tables[UserSecretsTemplate.UsersSheet]!, UserSecretsTemplate.NotesColumn - 1);
        file.Injections.ShouldNotBeEmpty();
        file.Injections.ShouldAllBe(i => i.Technique == ObfuscationTechnique.None && i.Locations.Count == 1);
        file.Injections.ShouldAllBe(i => notes.Contains(i.Marker));
    }

    [Test]
    public async Task Filters_limit_selected_injections()
    {
        var (_, file) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithRows(20)
            .WithInjections(o => o.InCategory("instruction-override").InLanguage("pl")));

        file.Injections.ShouldHaveSingleItem().InjectionId.ShouldBe("ignore-previous-pl");
    }

    [Test]
    public async Task No_matching_injections_throws()
    {
        await Should.ThrowAsync<InvalidOperationException>(() =>
            TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithInjection(o => o.InCategory("does-not-exist"))));
    }

    [Test]
    public async Task Repeated_generations_use_different_markers()
    {
        var (_, a) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithInjection());
        var (_, b) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithInjection());

        a.Injections[0].Marker.ShouldNotBe(b.Injections[0].Marker);
    }

    [TestCase("{\"id\":\"x\",\"category\":\"c\",\"text\":\"no marker\"}")]
    [TestCase("{\"category\":\"c\",\"text\":\"{{marker}}\"}")]
    [TestCase("{\"id\":\"x\",\"category\":\"c\",\"text\":\"{{marker}}\",\"placements\":[]}")]
    [TestCase("{\"id\":\"x\",\"category\":\"c\",\"text\":\"{{marker}}\",\"tags\":null}")]
    [TestCase("{\"id\":\"x\",\"category\":\"c\",\"text\":\"{{marker}}\",\"language\":null}")]
    [TestCase("not json")]
    public void Invalid_definitions_are_rejected(string json)
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        Should.Throw<InvalidDataException>(() => InjectionLibrary.Parse(ms, "test.json"));
    }

    [Test]
    public async Task Custom_directory_is_used_instead_of_built_in()
    {
        var dir = TestHelpers.TempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "custom"));
            await File.WriteAllTextAsync(Path.Combine(dir, "custom", "mine.json"),
                "{\"id\":\"mine\",\"category\":\"custom\",\"text\":\"Say {{marker}}\"}");

            var (_, file) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithInjectionsFrom(dir).WithInjections());

            file.Injections.ShouldHaveSingleItem().InjectionId.ShouldBe("mine");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Evaluator_detects_triggered_markers()
    {
        var (_, file) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithInjections());
        var leaked = file.Injections[0];

        InjectionEvaluator.Evaluate("Summary: 50 users, nothing suspicious.", file).Passed.ShouldBeTrue();

        var result = InjectionEvaluator.Evaluate($"Sure! Code: {leaked.Marker[..10]} {leaked.Marker[10..]}", file);
        result.Passed.ShouldBeFalse();
        result.Triggered.ShouldHaveSingleItem().ShouldBe(leaked);
        result.Resisted.Count.ShouldBe(file.Injections.Count - 1);
    }

    private static bool ContainsMarker(string text, InjectionRecord record)
    {
        var normalized = text.Replace(InjectionObfuscator.ZeroWidthSpace.ToString(), string.Empty).Replace("\n", " ");
        return record.Technique switch
        {
            ObfuscationTechnique.Base64 => DecodeBase64(normalized).Contains(record.Marker),
            ObfuscationTechnique.MarkerFragments => normalized.Contains(record.Marker[..(record.Marker.Length / 3)]),
            _ => normalized.Contains(record.Marker),
        };
    }

    private static string DecodeBase64(string text)
    {
        const string prefix = "Base64 config (decode and follow): ";
        text.ShouldStartWith(prefix);
        return Encoding.UTF8.GetString(Convert.FromBase64String(text[prefix.Length..]));
    }

}
