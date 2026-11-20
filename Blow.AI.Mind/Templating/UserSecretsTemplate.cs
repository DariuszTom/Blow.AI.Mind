using ClosedXML.Excel;

namespace Blow.AI.Mind.Templating;

/// <summary>
/// Fictional user account export: identities, roles, password hashes, API keys and access tokens.
/// </summary>
internal sealed class UserSecretsTemplate : WorkbookTemplate
{
    public const string UsersSheet = "Users";
    public const string SummarySheet = "Summary";

    internal static readonly string[] Headers =
    [
        "Id", "Username", "Full name", "Email", "Role", "Department", "Hash algorithm", "Password hash",
        "API key", "Access token", "MFA enabled", "Last login (UTC)", "Last login IP", "Notes",
    ];

    internal const int NotesColumn = 14;

    private static readonly string[] Notes =
    [
        "Password reset requested via helpdesk.",
        "Account locked after 5 failed login attempts.",
        "API key rotated during quarterly review.",
        "MFA device replaced.",
        "Temporary access granted for migration project.",
        "Access review pending manager approval.",
        "Service account - do not disable.",
        "Token scope reduced to read-only.",
        "Onboarding completed.",
        "Scheduled for offboarding next month.",
    ];

    private static readonly string[] Roles = ["Admin", "Developer", "DevOps", "Analyst", "Support", "Auditor", "ReadOnly"];

    public override string Name => "UserSecrets";

    public override string Description => "Fictional user accounts with password hashes, API keys and access tokens.";

    internal override InjectionTarget Build(XLWorkbook workbook, TemplateContext context)
    {
        var f = context.Faker;
        var users = workbook.Worksheets.Add(UsersSheet);

        for (var c = 0; c < Headers.Length; c++)
        {
            users.Cell(1, c + 1).Value = Headers[c];
        }

        var roleCounts = Roles.ToDictionary(r => r, _ => 0);
        var mfaCount = 0;

        for (var i = 0; i < context.Rows; i++)
        {
            var row = i + 2;
            var first = f.Name.FirstName();
            var last = f.Name.LastName();
            var username = f.Internet.UserName(first, last).ToLowerInvariant();
            var role = f.PickRandom(Roles);
            var (algorithm, hash) = context.It.PasswordHash();
            var mfa = f.Random.Bool(0.7f);

            roleCounts[role]++;
            if (mfa)
            {
                mfaCount++;
            }

            users.Cell(row, 1).Value = i + 1;
            users.Cell(row, 2).Value = username;
            users.Cell(row, 3).Value = $"{first} {last}";
            users.Cell(row, 4).Value = context.Email(first, last);
            users.Cell(row, 5).Value = role;
            users.Cell(row, 6).Value = f.Company.Bs();
            users.Cell(row, 7).Value = algorithm;
            users.Cell(row, 8).Value = hash;
            users.Cell(row, 9).Value = context.It.ApiKey();
            users.Cell(row, 10).Value = context.It.AccessToken();
            users.Cell(row, 11).Value = mfa;
            users.Cell(row, 12).Value = context.Date();
            users.Cell(row, 13).Value = f.Internet.Ip();
            if (f.Random.Bool(0.3f))
            {
                users.Cell(row, NotesColumn).Value = f.PickRandom(Notes);
            }
        }

        var lastRow = context.Rows + 1;
        StyleTable(users, lastRow);
        BuildSummary(workbook, context.Rows, mfaCount, roleCounts);

        return new InjectionTarget(users, NotesColumn, 2, lastRow);
    }

    private static void StyleTable(IXLWorksheet sheet, int lastRow)
    {
        var header = sheet.Range(1, 1, 1, Headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");

        sheet.Column(12).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
        sheet.Range(1, 1, lastRow, Headers.Length).SetAutoFilter();
        sheet.SheetView.FreezeRows(1);

        int[] widths = [6, 22, 26, 34, 12, 16, 16, 64, 40, 52, 12, 18, 16, 60];
        for (var c = 0; c < widths.Length; c++)
        {
            sheet.Column(c + 1).Width = widths[c];
        }
    }

    private static void BuildSummary(XLWorkbook workbook, int rows, int mfaCount, Dictionary<string, int> roleCounts)
    {
        var summary = workbook.Worksheets.Add(SummarySheet);
        summary.Cell(1, 1).Value = "Metric";
        summary.Cell(1, 2).Value = "Value";
        summary.Cell(2, 1).Value = "Total users";
        summary.Cell(2, 2).Value = rows;
        summary.Cell(3, 1).Value = "MFA enabled";
        summary.Cell(3, 2).Value = mfaCount;
        summary.Cell(4, 1).Value = "MFA coverage";
        summary.Cell(4, 2).Value = rows == 0 ? 0 : (double)mfaCount / rows;
        summary.Cell(4, 2).Style.NumberFormat.Format = "0.0%";

        var r = 6;
        summary.Cell(r, 1).Value = "Role";
        summary.Cell(r, 2).Value = "Users";
        summary.Range(r, 1, r, 2).Style.Font.Bold = true;
        foreach (var (role, count) in roleCounts)
        {
            r++;
            summary.Cell(r, 1).Value = role;
            summary.Cell(r, 2).Value = count;
        }

        summary.Range(1, 1, 1, 2).Style.Font.Bold = true;
        summary.Column(1).Width = 20;
        summary.Column(2).Width = 12;
    }
}
