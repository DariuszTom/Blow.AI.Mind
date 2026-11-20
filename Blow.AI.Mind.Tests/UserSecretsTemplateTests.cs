using Blow.AI.Mind.FakeData;
using Blow.AI.Mind.Templating;

namespace Blow.AI.Mind.Tests;

public class UserSecretsTemplateTests
{
    [Test]
    public async Task Generates_users_sheet_with_expected_columns_and_rows()
    {
        var (bytes, file) = await TestHelpers.GenerateAsync(
            WorkbookGenerator.Create().WithTemplate(Templates.UserSecrets).WithRows(25));

        var data = TestHelpers.ReadExcel(bytes);
        var users = data.Tables[UserSecretsTemplate.UsersSheet]!;

        users.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName).ShouldBe(UserSecretsTemplate.Headers);
        users.Rows.Count.ShouldBe(25);
        data.Tables.Contains(UserSecretsTemplate.SummarySheet).ShouldBeTrue();
        file.Encrypted.ShouldBeFalse();
        file.Injections.ShouldBeEmpty();
    }

    [Test]
    public async Task Secrets_use_fake_prefixes_and_typed_values()
    {
        var (bytes, _) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithRows(50));
        var users = TestHelpers.ReadExcel(bytes).Tables[UserSecretsTemplate.UsersSheet]!;

        foreach (System.Data.DataRow row in users.Rows)
        {
            row["API key"].ToString()!.ShouldStartWith(ItFakeData.ApiKeyPrefix);
            row["Access token"].ToString()!.ShouldStartWith(ItFakeData.TokenPrefix);
            row["MFA enabled"].ShouldBeOfType<bool>();
            row["Last login (UTC)"].ShouldBeOfType<DateTime>();
            row["Id"].ShouldBeOfType<double>();
        }
    }

    [Test]
    public async Task Repeated_generations_produce_different_data()
    {
        var (a, _) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithRows(30));
        var (b, _) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithRows(30));

        Dump(a).ShouldNotBe(Dump(b));
    }

    [Test]
    public void Templates_registry_contains_user_secrets()
    {
        Templates.All.ShouldBe([Templates.UserSecrets]);
        Templates.Find("usersecrets").ShouldBeSameAs(Templates.UserSecrets);
    }

    private static string Dump(byte[] bytes)
    {
        var table = TestHelpers.ReadExcel(bytes).Tables[UserSecretsTemplate.UsersSheet]!;
        return string.Join("\n", table.Rows.Cast<System.Data.DataRow>().Select(r => string.Join("|", r.ItemArray)));
    }
}
