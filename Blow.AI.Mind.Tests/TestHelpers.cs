using System.Data;
using System.Text;
using ExcelDataReader;

namespace Blow.AI.Mind.Tests;

internal static class TestHelpers
{
    static TestHelpers() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static DataSet ReadExcel(byte[] bytes, string? password = null)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = ExcelReaderFactory.CreateReader(stream, new ExcelReaderConfiguration { Password = password });
        return reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true },
        });
    }

    public static async Task<(byte[] Bytes, GeneratedFile File)> GenerateAsync(WorkbookGenerator generator)
    {
        using var ms = new MemoryStream();
        var file = await generator.GenerateAsync(ms);
        return (ms.ToArray(), file);
    }

    public static string CellsText(DataTable table, int column) =>
        string.Join("\n", table.Rows.Cast<DataRow>().Select(r => r[column]?.ToString()));

    public static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "bam-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
