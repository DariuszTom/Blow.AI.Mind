using Bogus;
using Blow.AI.Mind.Templating;
using ClosedXML.Excel;

namespace Blow.AI.Mind.Injections;

internal sealed record PlannedInjection(InjectionDefinition Definition, InjectionPlacement Placement, string Marker, Uri? BeaconUrl, ObfuscatedPayload Payload);

/// <summary>Writes injection payloads into the requested workbook locations.</summary>
internal sealed class InjectionPlanter(XLWorkbook workbook, InjectionTarget target, Randomizer random)
{
    internal const string HiddenSheetName = "Settings";
    internal const string PropertyPrefix = "Review note ";
    internal const string DefinedNamePrefix = "_ReviewNote";
    internal const string CommentAuthor = "IT Operations";

    /// <summary>Excel limit for a single string literal inside a formula.</summary>
    private const int MaxFormulaLiteral = 255;

    private static readonly (string Key, string Value)[] SettingsRows =
    [
        ("Export format", "xlsx"),
        ("Export version", "2.4.1"),
        ("Source system", "IAM directory"),
        ("Retention (days)", "90"),
    ];

    private IXLWorksheet? _hiddenSheet;
    private int _hiddenSheetRow;
    private int _propertyIndex;
    private int _definedNameIndex;

    public List<InjectionRecord> Plant(IReadOnlyList<PlannedInjection> items, Difficulty difficulty)
    {
        var locations = new Dictionary<PlannedInjection, (string? Sheet, List<string> Locations)>();

        foreach (var group in items.Where(i => ColumnOf(i.Placement) is not null).GroupBy(i => ColumnOf(i.Placement)!.Value))
        {
            PlantInColumn(group.Key, [.. group], locations);
        }

        foreach (var item in items.Where(i => ColumnOf(i.Placement) is null))
        {
            locations[item] = item.Placement switch
            {
                InjectionPlacement.HiddenSheet => (HiddenSheetName, PlantInHiddenSheet(item.Payload)),
                InjectionPlacement.DocumentProperties => (null, PlantInProperties(item.Payload)),
                InjectionPlacement.DefinedName => (null, PlantInDefinedNames(item.Payload)),
                _ => throw new InvalidOperationException($"Unsupported placement {item.Placement}."),
            };
        }

        return [.. items.Select(i => new InjectionRecord(i.Definition.Id, i.Definition.Category, i.Marker, difficulty,
            i.Payload.Technique, i.Placement, locations[i].Sheet, locations[i].Locations, i.BeaconUrl))];
    }

    private int? ColumnOf(InjectionPlacement placement) => placement switch
    {
        InjectionPlacement.Cell => target.Column,
        InjectionPlacement.HiddenCell => target.HiddenColumn,
        InjectionPlacement.Comment => target.CommentColumn,
        _ => null,
    };

    private void PlantInColumn(int column, List<PlannedInjection> items, Dictionary<PlannedInjection, (string?, List<string>)> locations)
    {
        var required = items.Sum(i => i.Payload.Cells.Count);
        var lastStart = target.LastRow - required + 1;
        if (lastStart < target.FirstRow)
        {
            throw new InvalidOperationException(
                $"Not enough rows ({target.LastRow - target.FirstRow + 1}) to place {items.Count} injection(s) in column {column}. Increase WithRows().");
        }

        var row = random.Number(target.FirstRow, lastStart);
        foreach (var item in items)
        {
            var cells = new List<string>(item.Payload.Cells.Count);
            foreach (var part in item.Payload.Cells)
            {
                var cell = target.Sheet.Cell(row++, column);
                switch (item.Placement)
                {
                    case InjectionPlacement.Comment:
                        var comment = cell.CreateComment();
                        comment.Author = CommentAuthor;
                        comment.AddText(part);
                        break;
                    case InjectionPlacement.HiddenCell:
                        cell.Value = part;
                        cell.Style.Font.FontColor = XLColor.White;
                        target.Sheet.Column(column).Hide();
                        break;
                    default:
                        cell.Value = part;
                        break;
                }

                cells.Add(cell.Address.ToStringRelative());
            }

            locations[item] = (target.Sheet.Name, cells);
        }
    }

    private List<string> PlantInHiddenSheet(ObfuscatedPayload payload)
    {
        if (_hiddenSheet is null)
        {
            _hiddenSheet = workbook.Worksheets.Add(HiddenSheetName);
            _hiddenSheet.Cell(1, 1).Value = "Key";
            _hiddenSheet.Cell(1, 2).Value = "Value";
            _hiddenSheetRow = 2;
            foreach (var (key, value) in SettingsRows)
            {
                _hiddenSheet.Cell(_hiddenSheetRow, 1).Value = key;
                _hiddenSheet.Cell(_hiddenSheetRow++, 2).Value = value;
            }

            _hiddenSheet.Visibility = XLWorksheetVisibility.Hidden;
        }

        var cells = new List<string>(payload.Cells.Count);
        foreach (var part in payload.Cells)
        {
            _hiddenSheet.Cell(_hiddenSheetRow, 1).Value = "Note";
            var cell = _hiddenSheet.Cell(_hiddenSheetRow++, 2);
            cell.Value = part;
            cells.Add(cell.Address.ToStringRelative());
        }

        return cells;
    }

    private List<string> PlantInProperties(ObfuscatedPayload payload)
    {
        var names = new List<string>(payload.Cells.Count);
        foreach (var part in payload.Cells)
        {
            var name = PropertyPrefix + ++_propertyIndex;
            workbook.CustomProperties.Add(name, part);
            names.Add(name);
        }

        return names;
    }

    private List<string> PlantInDefinedNames(ObfuscatedPayload payload)
    {
        var names = new List<string>(payload.Cells.Count);
        foreach (var part in payload.Cells)
        {
            var name = DefinedNamePrefix + ++_definedNameIndex;
            workbook.DefinedNames.Add(name, ToFormulaLiteral(part));
            names.Add(name);
        }

        return names;
    }

    /// <summary>Builds <c>="..."&amp;"..."</c>, splitting the text into chunks that respect the formula literal limit.</summary>
    internal static string ToFormulaLiteral(string text) =>
        "=" + string.Join("&", text.Chunk(MaxFormulaLiteral).Select(c => "\"" + new string(c).Replace("\"", "\"\"", StringComparison.Ordinal) + "\""));
}
