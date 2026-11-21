using OpenMcdf;
using System.Buffers.Binary;

namespace Blow.AI.Mind.Encryption;

/// <summary>
/// ECMA-376 Agile Encryption (AES-256-CBC, SHA-512, 100 000 spins) - the format used by Excel 2010+ for "Encrypt with password".
/// </summary>
internal static class AgileEncryptor
{
    private const int SpinCount = 100_000;
    private const int KeyBytes = 32;
    private const int BlockSize = 16;
    private const int SaltSize = 16;
    private const int HashSize = 64;
    private const int SegmentLength = 4096;
    private const string PasswordKeyEncryptorUri = "http://schemas.microsoft.com/office/2006/keyEncryptor/password";

    private static readonly byte[] BlockKeyVerifierHashInput = [0xFE, 0xA7, 0xD2, 0x76, 0x3B, 0x4B, 0x9E, 0x79];
    private static readonly byte[] BlockKeyVerifierHashValue = [0xD7, 0xAA, 0x0F, 0x6D, 0x30, 0x61, 0x34, 0x4E];
    private static readonly byte[] BlockKeyEncryptedKey = [0x14, 0x6E, 0x0B, 0xE7, 0xAB, 0xAC, 0xD0, 0xD6];
    private static readonly byte[] BlockKeyIntegrityKey = [0x5F, 0xB2, 0xAD, 0x01, 0x0C, 0xB9, 0xE1, 0xF6];
    private static readonly byte[] BlockKeyIntegrityValue = [0xA0, 0x67, 0x7F, 0x02, 0xB2, 0x2C, 0x84, 0x33];

    private static readonly byte[] VersionStream = Convert.FromHexString(
        "3C0000004D006900630072006F0073006F00660074002E0043006F006E007400610069006E00650072002E004400610074006100530070006100630065007300010000000100000001000000");

    private static readonly byte[] DataSpaceMapStream = Convert.FromHexString(
        "08000000010000006800000001000000000000002000000045006E0063007200790070007400650064005000610063006B00610067006500320000005300740072006F006E00670045006E006300720079007000740069006F006E004400610074006100530070006100630065000000");

    private static readonly byte[] StrongEncryptionDataSpaceStream = Convert.FromHexString(
        "0800000001000000320000005300740072006F006E00670045006E006300720079007000740069006F006E005400720061006E00730066006F0072006D000000");

    private static readonly byte[] PrimaryStream = Convert.FromHexString(
        "58000000010000004C0000007B00460046003900410033004600300033002D0035003600450046002D0034003600310033002D0042004400440035002D003500410034003100430031004400300037003200340036007D004E0000004D006900630072006F0073006F00660074002E0043006F006E007400610069006E00650072002E0045006E006300720079007000740069006F006E005400720061006E00730066006F0072006D00000001000000010000000100000000000000000000000000000004000000");

    /// <summary>Encrypts an OOXML package (e.g. .xlsx bytes) into a CFB container written to <paramref name="output"/>.</summary>
    public static void Encrypt(ReadOnlySpan<byte> package, Stream output, string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var secretKey = RandomNumberGenerator.GetBytes(KeyBytes);
        var keyDataSalt = RandomNumberGenerator.GetBytes(SaltSize);
        var passwordSalt = RandomNumberGenerator.GetBytes(SaltSize);

        var encryptedPackage = EncryptPackage(package, secretKey, keyDataSalt);

        var hmacKey = RandomNumberGenerator.GetBytes(HashSize);
        var hmacValue = HMACSHA512.HashData(hmacKey, encryptedPackage);
        var encryptedHmacKey = AesCbc(secretKey, DeriveIv(keyDataSalt, BlockKeyIntegrityKey), hmacKey);
        var encryptedHmacValue = AesCbc(secretKey, DeriveIv(keyDataSalt, BlockKeyIntegrityValue), hmacValue);

        var passwordHash = HashPassword(password, passwordSalt);
        var verifierInput = RandomNumberGenerator.GetBytes(SaltSize);
        var encryptedVerifierInput = AesCbc(DeriveKey(passwordHash, BlockKeyVerifierHashInput), passwordSalt, verifierInput);
        var encryptedVerifierValue = AesCbc(DeriveKey(passwordHash, BlockKeyVerifierHashValue), passwordSalt, SHA512.HashData(verifierInput));
        var encryptedKeyValue = AesCbc(DeriveKey(passwordHash, BlockKeyEncryptedKey), passwordSalt, secretKey);

        var info = BuildEncryptionInfo(keyDataSalt, encryptedHmacKey, encryptedHmacValue, passwordSalt,
            encryptedVerifierInput, encryptedVerifierValue, encryptedKeyValue);

        WriteContainer(output, info, encryptedPackage);

        CryptographicOperations.ZeroMemory(secretKey);
        CryptographicOperations.ZeroMemory(passwordHash);
        CryptographicOperations.ZeroMemory(hmacKey);
    }

    private static byte[] EncryptPackage(ReadOnlySpan<byte> package, byte[] secretKey, byte[] keyDataSalt)
    {
        var segments = (package.Length + SegmentLength - 1) / SegmentLength;
        using var ms = new MemoryStream(8 + package.Length + segments * BlockSize);
        Span<byte> size = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(size, (ulong)package.Length);
        ms.Write(size);

        using var aes = Aes.Create();
        aes.Key = secretKey;
        Span<byte> blockKey = stackalloc byte[4];
        for (var i = 0; i < segments; i++)
        {
            var chunk = package.Slice(i * SegmentLength, Math.Min(SegmentLength, package.Length - i * SegmentLength));
            BinaryPrimitives.WriteUInt32LittleEndian(blockKey, (uint)i);
            var iv = DeriveIv(keyDataSalt, blockKey.ToArray());
            ms.Write(aes.EncryptCbc(PadToBlock(chunk), iv, PaddingMode.None));
        }

        return ms.ToArray();
    }

    private static byte[] HashPassword(string password, byte[] salt)
    {
        var hash = SHA512.HashData([.. salt, .. Encoding.Unicode.GetBytes(password)]);
        var buffer = new byte[4 + hash.Length];
        for (var i = 0; i < SpinCount; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, (uint)i);
            hash.CopyTo(buffer, 4);
            SHA512.HashData(buffer, hash);
        }

        return hash;
    }

    private static byte[] DeriveKey(byte[] passwordHash, byte[] blockKey) =>
        SHA512.HashData([.. passwordHash, .. blockKey])[..KeyBytes];

    private static byte[] DeriveIv(byte[] salt, byte[] blockKey) =>
        SHA512.HashData([.. salt, .. blockKey])[..BlockSize];

    private static byte[] AesCbc(byte[] key, byte[] iv, ReadOnlySpan<byte> data)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        return aes.EncryptCbc(PadToBlock(data), iv, PaddingMode.None);
    }

    private static byte[] PadToBlock(ReadOnlySpan<byte> data)
    {
        var padded = new byte[(data.Length + BlockSize - 1) / BlockSize * BlockSize];
        data.CopyTo(padded);
        return padded;
    }

    private static byte[] BuildEncryptionInfo(byte[] keyDataSalt, byte[] encryptedHmacKey, byte[] encryptedHmacValue,
        byte[] passwordSalt, byte[] verifierInput, byte[] verifierValue, byte[] keyValue)
    {
        var common = $"blockSize=\"{BlockSize}\" keyBits=\"{KeyBytes * 8}\" hashSize=\"{HashSize}\" cipherAlgorithm=\"AES\" cipherChaining=\"ChainingModeCBC\" hashAlgorithm=\"SHA512\"";
        var xml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
            "<encryption xmlns=\"http://schemas.microsoft.com/office/2006/encryption\" " +
            $"xmlns:p=\"{PasswordKeyEncryptorUri}\" " +
            "xmlns:c=\"http://schemas.microsoft.com/office/2006/keyEncryptor/certificate\">" +
            $"<keyData saltSize=\"{SaltSize}\" {common} saltValue=\"{B64(keyDataSalt)}\"/>" +
            $"<dataIntegrity encryptedHmacKey=\"{B64(encryptedHmacKey)}\" encryptedHmacValue=\"{B64(encryptedHmacValue)}\"/>" +
            $"<keyEncryptors><keyEncryptor uri=\"{PasswordKeyEncryptorUri}\">" +
            $"<p:encryptedKey spinCount=\"{SpinCount}\" saltSize=\"{SaltSize}\" {common} saltValue=\"{B64(passwordSalt)}\" " +
            $"encryptedVerifierHashInput=\"{B64(verifierInput)}\" encryptedVerifierHashValue=\"{B64(verifierValue)}\" " +
            $"encryptedKeyValue=\"{B64(keyValue)}\"/>" +
            "</keyEncryptor></keyEncryptors></encryption>";

        byte[] header = [0x04, 0x00, 0x04, 0x00, 0x40, 0x00, 0x00, 0x00];
        return [.. header, .. Encoding.UTF8.GetBytes(xml)];
    }

    private static string B64(byte[] data) => Convert.ToBase64String(data);

    private static void WriteContainer(Stream output, byte[] encryptionInfo, byte[] encryptedPackage)
    {
        using var root = RootStorage.Create(output, OpenMcdf.Version.V3, StorageModeFlags.LeaveOpen);

        WriteStream(root, "EncryptionInfo", encryptionInfo);
        WriteStream(root, "EncryptedPackage", encryptedPackage);

        var dataSpaces = root.CreateStorage("\u0006DataSpaces");
        WriteStream(dataSpaces, "Version", VersionStream);
        WriteStream(dataSpaces, "DataSpaceMap", DataSpaceMapStream);
        WriteStream(dataSpaces.CreateStorage("DataSpaceInfo"), "StrongEncryptionDataSpace", StrongEncryptionDataSpaceStream);
        WriteStream(dataSpaces.CreateStorage("TransformInfo").CreateStorage("StrongEncryptionTransform"), "\u0006Primary", PrimaryStream);

        root.Flush(true);
    }

    private static void WriteStream(Storage storage, string name, byte[] data)
    {
        using var stream = storage.CreateStream(name);
        stream.Write(data);
    }
}
