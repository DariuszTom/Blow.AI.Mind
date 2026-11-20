namespace Blow.AI.Mind.Tests;

public class GeneratorTests
{
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(WorkbookGenerator.MaxRows + 1)]
    public void Invalid_row_count_throws(int rows) =>
        Should.Throw<ArgumentOutOfRangeException>(() => WorkbookGenerator.Create().WithRows(rows));

    [Test]
    public void Unknown_locale_throws() =>
        Should.Throw<ArgumentException>(() => WorkbookGenerator.Create().WithLocale("xx-nope"));

    [Test]
    public void Null_template_throws() =>
        Should.Throw<ArgumentNullException>(() => WorkbookGenerator.Create().WithTemplate(null!));

    [Test]
    public void Missing_injection_directory_throws() =>
        Should.Throw<DirectoryNotFoundException>(() => WorkbookGenerator.Create().WithInjectionsFrom(@"Z:\does\not\exist"));

    [Test]
    public Task Too_few_rows_for_all_injections_throws() =>
        Should.ThrowAsync<InvalidOperationException>(() =>
            TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithRows(1).WithInjections()));

    [Test]
    public async Task Generates_multiple_files_with_unique_names()
    {
        var dir = TestHelpers.TempDirectory();
        try
        {
            var generator = WorkbookGenerator.Create().WithRows(5);
            var result = await generator.GenerateAsync(dir, count: 3);

            result.Files.Count.ShouldBe(3);
            result.Files.Select(f => f.Path).ShouldBeUnique();
            result.Files.ShouldAllBe(f => File.Exists(f.Path));
            Directory.GetFiles(dir, "*.tmp").ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
