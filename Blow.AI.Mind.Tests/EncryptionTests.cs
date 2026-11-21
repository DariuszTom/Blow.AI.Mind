using Blow.AI.Mind.Templating;
using System.Text.Json;

namespace Blow.AI.Mind.Tests;

public class EncryptionTests
{
    [Test]
    public async Task Encrypted_file_opens_only_with_the_discarded_password()
    {
        string? password = null;
        var generator = WorkbookGenerator.Create().WithRows(30).WithInjection().Encrypted();
        generator.PasswordObserver = (_, p) => password = p;

        var (bytes, file) = await TestHelpers.GenerateAsync(generator);

        file.Encrypted.ShouldBeTrue();
        password.ShouldNotBeNullOrEmpty();
        bytes.AsSpan(0, 8).ToArray().ShouldBe([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]);

        Should.Throw<Exception>(() => TestHelpers.ReadExcel(bytes));
        Should.Throw<Exception>(() => TestHelpers.ReadExcel(bytes, "wrong-password"));

        var users = TestHelpers.ReadExcel(bytes, password).Tables[UserSecretsTemplate.UsersSheet]!;
        users.Rows.Count.ShouldBe(30);
    }

    [Test]
    public async Task Each_file_gets_a_different_password()
    {
        var passwords = new List<string>();
        var generator = WorkbookGenerator.Create().WithRows(5).Encrypted();
        generator.PasswordObserver = (_, p) => passwords.Add(p);

        await TestHelpers.GenerateAsync(generator);
        await TestHelpers.GenerateAsync(generator);

        passwords.Count.ShouldBe(2);
        passwords[0].ShouldNotBe(passwords[1]);
    }

    [Test]
    public async Task Manifest_never_contains_the_password()
    {
        string? password = null;
        var dir = TestHelpers.TempDirectory();
        try
        {
            var generator = WorkbookGenerator.Create().WithRows(80).WithInjections().Encrypted();
            generator.PasswordObserver = (_, p) => password = p;

            var result = await generator.GenerateAsync(dir, count: 1);

            JsonSerializer.Serialize(result).ShouldNotContain(password!);
            typeof(GeneratedFile).GetProperties().ShouldNotContain(p => p.Name.Contains("Password"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
