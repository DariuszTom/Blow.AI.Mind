using DocumentFormat.OpenXml.Office.Word;
using System.Security.Cryptography;

namespace Blow.AI.Mind.Encryption;

/// <summary>Cryptographically random passwords for encrypted workbooks.</summary>
internal static class PasswordGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*-_=+";
    /// <summary>
    /// Znaki	Kombinacje	Average (1 GPU)
    /// 6	    23 mln	    9 days
    /// </summary>
    /// <param name="length"></param>
    /// <returns></returns>
    public static string Create(int length = 6) => RandomNumberGenerator.GetString(Alphabet, length);
}
