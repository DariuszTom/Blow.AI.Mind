using System.Security.Cryptography;

namespace Blow.AI.Mind.Encryption;

/// <summary>Cryptographically random passwords for encrypted workbooks.</summary>
internal static class PasswordGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*-_=+";

    public static string Create(int length = 32) => RandomNumberGenerator.GetString(Alphabet, length);
}
