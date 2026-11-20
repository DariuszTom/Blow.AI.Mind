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
/// Free-text column of a template where injection scenarios may be placed.
/// </summary>
internal sealed record InjectionTarget(IXLWorksheet Sheet, int Column, int FirstRow, int LastRow);
