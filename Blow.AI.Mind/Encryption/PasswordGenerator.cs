namespace Blow.AI.Mind.Encryption;

/// <summary>Cryptographically random passwords for encrypted workbooks.</summary>
internal static class PasswordGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*-_=+";
    /// <summary>
    /// Characters	Combinations	Average (1 GPU)
    /// 6	        23 mln	        9 days
    /// </summary>
    /// <param name="length"></param>
    /// <returns></returns>
    public static string Create(int length = 0) => RandomNumberGenerator.GetString(Alphabet, length = length == 0 ? RandomNumberGenerator.GetInt32(4, 6) : length);
}
