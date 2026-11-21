namespace Blow.AI.Mind.FakeData;

/// <summary>
/// IT-flavoured fictional values (hashes, keys, tokens). Every secret-like value carries
/// an explicit fictional prefix so that secret scanners and humans can recognise it as fake.
/// </summary>
internal sealed class ItFakeData(Randomizer random)
{
    public const string ApiKeyPrefix = "glpat-";
    public const string TokenPrefix = "bam_test_";

    private const string Alphanumeric = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const string BcryptAlphabet = "./ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public string Md5Hex() => Convert.ToHexStringLower(MD5.HashData(random.Bytes(32)));

    public string Sha1Hex() => Convert.ToHexStringLower(SHA1.HashData(random.Bytes(32)));

    public string Sha256Hex() => Convert.ToHexStringLower(SHA256.HashData(random.Bytes(32)));

    /// <summary>Returns a password hash in a randomly chosen, realistic-looking format and its algorithm name.</summary>
    public (string Algorithm, string Hash) PasswordHash()
    {
        return random.Number(0, 2) switch
        {
            0 => ("bcrypt", $"$2b${random.Number(10, 12)}${random.String2(53, BcryptAlphabet)}"),
            1 => ("pbkdf2-sha256",
                $"pbkdf2_sha256$600000${Convert.ToBase64String(random.Bytes(12))}${Convert.ToBase64String(SHA256.HashData(random.Bytes(32)))}"),
            _ => ("sha256-salted", $"{Convert.ToHexStringLower(random.Bytes(8))}:{Sha256Hex()}"),
        };
    }

    public string ApiKey() => ApiKeyPrefix + random.String2(32, Alphanumeric);

    public string AccessToken() => TokenPrefix + random.String2(40, Alphanumeric);
}
