using ClosedXML.Excel;

namespace Blow.AI.Mind.Templating;

/// <summary>
/// Describes a workbook layout filled with fictional data. Use the instances exposed by <see cref="Templates"/>.
/// </summary>
public abstract class WorkbookTemplate
{
    private protected WorkbookTemplate()
    {
    }

    /// <summary>Stable, machine-friendly template name (used in file names, manifests and the CLI).</summary>
    public abstract string Name { get; }

    /// <summary>Human-readable description of the template.</summary>
    public abstract string Description { get; }

    internal abstract InjectionTarget Build(XLWorkbook workbook, TemplateContext context);

    public override string ToString() => Name;
}

/// <summary>
/// Locations of a template sheet where injection scenarios may be placed.
/// </summary>
/// <param name="Sheet">Worksheet that holds the data rows.</param>
/// <param name="Column">Visible free-text column.</param>
/// <param name="HiddenColumn">Empty column that is hidden when used.</param>
/// <param name="CommentColumn">Data column whose cells receive notes.</param>
/// <param name="FirstRow">First data row (1-based).</param>
/// <param name="LastRow">Last data row (1-based, inclusive).</param>
internal sealed record InjectionTarget(IXLWorksheet Sheet, int Column, int HiddenColumn, int CommentColumn, int FirstRow, int LastRow);
