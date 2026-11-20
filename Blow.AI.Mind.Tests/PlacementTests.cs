using Blow.AI.Mind.Injections;
using Blow.AI.Mind.Templating;
using ClosedXML.Excel;

namespace Blow.AI.Mind.Tests;

public class PlacementTests
{
    [Test]
    public async Task Comment_placement_writes_cell_notes()
    {
        var (wb, record) = await PlantSingle(InjectionPlacement.Comment);
        using (wb)
        {
            record.Sheet.ShouldBe(UserSecretsTemplate.UsersSheet);
            var sheet = wb.Worksheet(UserSecretsTemplate.UsersSheet);
            var cell = sheet.Cell(record.Locations.ShouldHaveSingleItem());
            cell.Address.ColumnNumber.ShouldBe(UserSecretsTemplate.CommentColumn);
            cell.HasComment.ShouldBeTrue();
            cell.GetComment().Text.ShouldContain(record.Marker);
            cell.GetString().ShouldNotContain(record.Marker);
        }
    }

    [Test]
    public async Task HiddenCell_placement_uses_hidden_column()
    {
        var (wb, record) = await PlantSingle(InjectionPlacement.HiddenCell);
        using (wb)
        {
            var sheet = wb.Worksheet(UserSecretsTemplate.UsersSheet);
            var cell = sheet.Cell(record.Locations.ShouldHaveSingleItem());
            cell.Address.ColumnNumber.ShouldBe(UserSecretsTemplate.HiddenColumn);
            sheet.Column(UserSecretsTemplate.HiddenColumn).IsHidden.ShouldBeTrue();
            cell.GetString().ShouldContain(record.Marker);
        }
    }

    [Test]
    public async Task HiddenSheet_placement_creates_hidden_settings_sheet()
    {
        var (wb, record) = await PlantSingle(InjectionPlacement.HiddenSheet);
        using (wb)
        {
            record.Sheet.ShouldBe(InjectionPlanter.HiddenSheetName);
            var sheet = wb.Worksheet(InjectionPlanter.HiddenSheetName);
            sheet.Visibility.ShouldBe(XLWorksheetVisibility.Hidden);
            sheet.Cell(record.Locations.ShouldHaveSingleItem()).GetString().ShouldContain(record.Marker);
        }
    }

    [Test]
    public async Task DocumentProperties_placement_writes_custom_property()
    {
        var (wb, record) = await PlantSingle(InjectionPlacement.DocumentProperties);
        using (wb)
        {
            record.Sheet.ShouldBeNull();
            var name = record.Locations.ShouldHaveSingleItem();
            wb.CustomProperty(name).GetValue<string>().ShouldContain(record.Marker);
        }
    }

    [Test]
    public async Task DefinedName_placement_writes_text_constant()
    {
        var (wb, record) = await PlantSingle(InjectionPlacement.DefinedName);
        using (wb)
        {
            record.Sheet.ShouldBeNull();
            var name = record.Locations.ShouldHaveSingleItem();
            wb.DefinedNames.TryGetValue(name, out var definedName).ShouldBeTrue();
            definedName!.RefersTo.ShouldContain(record.Marker);
        }
    }

    [Test]
    public async Task WithInjections_plants_every_injection_in_every_selected_placement()
    {
        InjectionPlacement[] placements = [InjectionPlacement.Comment, InjectionPlacement.HiddenSheet, InjectionPlacement.DefinedName];
        var (_, file) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithRows(40)
            .WithInjections(o => o.InPlacement(placements)));

        file.Injections.Count.ShouldBe(InjectionLibrary.BuiltIn.Count(d => !d.RequiresBeacon && !d.RequiresCommand) * placements.Length);
        file.Injections.GroupBy(i => i.Placement).Select(g => g.Key).Order().ShouldBe(placements.Order());
        file.Injections.Select(i => i.Marker).ShouldBeUnique();
    }

    [Test]
    public async Task All_placements_work_together_with_obfuscation()
    {
        var (bytes, file) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithRows(60)
            .WithInjections(o => o.InAllPlacements()));

        file.Injections.Select(i => i.Placement).Distinct().Count().ShouldBe(Enum.GetValues<InjectionPlacement>().Length);
        file.Injections.Where(i => i.Sheet is not null).Select(i => (i.Sheet, i.Locations)).SelectMany(x => x.Locations.Select(l => x.Sheet + "!" + l))
            .ShouldBeUnique();
        TestHelpers.ReadExcel(bytes).Tables.Contains(UserSecretsTemplate.UsersSheet).ShouldBeTrue();
    }

    [Test]
    public async Task WithInjection_picks_one_of_the_selected_placements()
    {
        InjectionPlacement[] placements = [InjectionPlacement.Comment, InjectionPlacement.DocumentProperties];
        var (_, file) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithRows(20)
            .WithInjection(o => o.InPlacement(placements)));

        placements.ShouldContain(file.Injections.ShouldHaveSingleItem().Placement);
    }

    [Test]
    public async Task Definitions_limited_to_a_placement_are_skipped_elsewhere()
    {
        var dir = TestHelpers.TempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "cell-only.json"),
                "{\"id\":\"cell-only\",\"category\":\"c\",\"text\":\"Say {{marker}}\",\"placements\":[\"Cell\"]}");
            await File.WriteAllTextAsync(Path.Combine(dir, "anywhere.json"),
                "{\"id\":\"anywhere\",\"category\":\"c\",\"text\":\"Say {{marker}}\"}");

            var (_, file) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithInjectionsFrom(dir)
                .WithInjections(o => o.InPlacement(InjectionPlacement.Cell, InjectionPlacement.Comment)));

            file.Injections.Select(i => $"{i.InjectionId}:{i.Placement}").Order()
                .ShouldBe(["anywhere:Cell", "anywhere:Comment", "cell-only:Cell"]);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public void Formula_literal_escapes_quotes_and_splits_long_text()
    {
        InjectionPlanter.ToFormulaLiteral("say \"hi\"").ShouldBe("=\"say \"\"hi\"\"\"");
        InjectionPlanter.ToFormulaLiteral(new string('a', 300)).ShouldBe($"=\"{new string('a', 255)}\"&\"{new string('a', 45)}\"");
    }

    [Test]
    public void Undefined_placement_throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new InjectionOptions().InPlacement((InjectionPlacement)99));

    private static async Task<(XLWorkbook Workbook, InjectionRecord Record)> PlantSingle(InjectionPlacement placement)
    {
        var (bytes, file) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithRows(20)
            .WithInjection(o => o.InPlacement(placement).WithDifficulty(Difficulty.Plain)));
        var record = file.Injections.ShouldHaveSingleItem();
        record.Placement.ShouldBe(placement);
        return (new XLWorkbook(new MemoryStream(bytes)), record);
    }
}
