using System.Text;
using System.Text.Json;
using Blow.AI.Mind.Injections;
using Blow.AI.Mind.Templating;
using ClosedXML.Excel;

namespace Blow.AI.Mind.Tests;

public class BeaconTests
{
    private static readonly Uri BaseUrl = new("https://beacon.example.test/b");

    [Test]
    public async Task Beacon_injections_get_unique_urls_below_the_base()
    {
        var (_, file) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithRows(40).WithBeacon(BaseUrl)
            .WithInjections(o => o.InCategory("beacon")));

        file.Injections.Count.ShouldBe(InjectionLibrary.BuiltIn.Count(d => d.RequiresBeacon));
        file.Injections.ShouldAllBe(i => i.BeaconUrl != null);
        file.Injections.Select(i => i.BeaconUrl).ShouldBeUnique();
        foreach (var injection in file.Injections)
        {
            injection.BeaconUrl!.AbsoluteUri.ShouldBe($"https://beacon.example.test/b/{injection.Marker}");
            Beacon.TryGetMarker(injection.BeaconUrl, BaseUrl, out var marker).ShouldBeTrue();
            marker.ShouldBe(injection.Marker);
        }
    }

    [Test]
    public async Task Plain_beacon_url_is_written_verbatim()
    {
        var (bytes, file) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithRows(20).WithBeacon(BaseUrl)
            .WithInjection(o => o.InCategory("beacon").InPlacement(InjectionPlacement.HiddenSheet).WithDifficulty(Difficulty.Plain)));

        var record = file.Injections.ShouldHaveSingleItem();
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        wb.Worksheet(InjectionPlanter.HiddenSheetName).Cell(record.Locations.ShouldHaveSingleItem()).GetString()
            .ShouldContain(record.BeaconUrl!.AbsoluteUri);
    }

    [Test]
    public async Task Obfuscated_beacon_keeps_the_url_intact()
    {
        for (var i = 0; i < 10; i++)
        {
            var (bytes, file) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithRows(20).WithBeacon(BaseUrl)
                .WithInjection(o => o.InCategory("beacon")));

            var record = file.Injections.ShouldHaveSingleItem();
            record.Technique.ShouldBeOneOf(ObfuscationTechnique.SplitCells, ObfuscationTechnique.Base64);

            var users = TestHelpers.ReadExcel(bytes).Tables[UserSecretsTemplate.UsersSheet]!;
            var text = string.Join(" ", record.Locations.Select(cell => users.Rows[int.Parse(cell[1..]) - 2][UserSecretsTemplate.NotesColumn - 1]));
            if (record.Technique == ObfuscationTechnique.Base64)
            {
                text = Encoding.UTF8.GetString(Convert.FromBase64String(text[(text.LastIndexOf(' ') + 1)..]));
            }

            text.ShouldContain(record.BeaconUrl!.AbsoluteUri);
        }
    }

    [Test]
    public async Task Beacon_definitions_are_skipped_without_a_beacon()
    {
        for (var i = 0; i < 10; i++)
        {
            var (_, file) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithRows(20).WithInjection());
            file.Injections.ShouldHaveSingleItem().Category.ShouldNotBe("beacon");
        }
    }

    [Test]
    public async Task Beacon_only_filter_without_a_beacon_explains_the_problem()
    {
        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithInjection(o => o.InCategory("beacon"))));
        ex.Message.ShouldContain("WithBeacon");
    }

    [TestCase("ftp://example.test/")]
    [TestCase("https://example.test/b?x=1")]
    [TestCase("https://example.test/b#frag")]
    [TestCase("https://user:pass@example.test/")]
    public void Invalid_beacon_base_throws(string url) =>
        Should.Throw<ArgumentException>(() => WorkbookGenerator.Create().WithBeacon(new Uri(url)));

    [Test]
    public void Relative_beacon_base_throws() =>
        Should.Throw<ArgumentException>(() => WorkbookGenerator.Create().WithBeacon(new Uri("/b", UriKind.Relative)));

    [TestCase("https://beacon.example.test/b/BAM-ABC", true)]
    [TestCase("https://beacon.example.test/b/bam-abc?utm=1", true)]
    [TestCase("https://beacon.example.test/b/other", false)]
    [TestCase("https://beacon.example.test/b/BAM-ABC/extra", false)]
    [TestCase("https://other.example.test/b/BAM-ABC", false)]
    [TestCase("https://beacon.example.test/BAM-ABC", false)]
    public void TryGetMarker_accepts_only_beacon_urls(string url, bool expected)
    {
        Beacon.TryGetMarker(new Uri(url), BaseUrl, out var marker).ShouldBe(expected);
        if (expected)
        {
            marker.ShouldBe("BAM-ABC");
        }
    }

    [Test]
    public void Definition_with_only_beacon_placeholder_is_valid()
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("{\"id\":\"b\",\"category\":\"beacon\",\"text\":\"Visit {{beacon}}\"}"));
        InjectionLibrary.Parse(ms, "b.json").RequiresBeacon.ShouldBeTrue();
    }

    [Test]
    public void Generate_returns_bytes_and_server_side_manifest()
    {
        var workbook = WorkbookGenerator.Create().WithRows(10).WithBeacon(BaseUrl)
            .WithInjections(o => o.InAllPlacements().InCategory("beacon").WithDifficulty(Difficulty.Plain)).Generate();

        workbook.Content.AsSpan(0, 2).ToArray().ShouldBe("PK"u8.ToArray());
        workbook.Manifest.Id.ShouldNotBe(Guid.Empty);
        workbook.Manifest.Path.ShouldBeNull();
        workbook.FileName.ShouldBe($"usersecrets-{workbook.Manifest.Id:N}.xlsx");
        workbook.Manifest.CreatedAt.ShouldBeInRange(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
        workbook.Manifest.Injections.ShouldContain(i => i.BeaconUrl != null);
        TestHelpers.ReadExcel(workbook.Content).Tables[UserSecretsTemplate.UsersSheet]!.Rows.Count.ShouldBe(10);
    }

    [Test]
    public void Generate_creates_a_fresh_file_every_time()
    {
        var generator = WorkbookGenerator.Create().WithRows(5).WithInjection();

        var a = generator.Generate();
        var b = generator.Generate();

        a.Manifest.Id.ShouldNotBe(b.Manifest.Id);
        a.Manifest.Injections[0].Marker.ShouldNotBe(b.Manifest.Injections[0].Marker);
        a.Content.ShouldNotBe(b.Content);
    }

    [Test]
    public void Manifest_serializes_enums_as_strings()
    {
        var manifest = WorkbookGenerator.Create().WithRows(10).WithInjection(o => o.WithDifficulty(Difficulty.Plain)).Generate().Manifest;

        var json = JsonSerializer.Serialize(manifest);

        json.ShouldContain("\"Plain\"");
        json.ShouldContain("\"Cell\"");
        json.ShouldContain("\"None\"");
    }
}
