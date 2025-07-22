using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

public static class CryptoUtils
{
    public static byte[] GetOrCreateSalt(string saltPath)
    {
        if (File.Exists(saltPath))
            return File.ReadAllBytes(saltPath);

        byte[] salt = new byte[24];
        RandomNumberGenerator.Fill(salt);
        File.WriteAllBytes(saltPath, salt);
        Debug.Log("🧂 Generated new salt.");
        return salt;
    }

    public static byte[] DeriveKey(string deviceId, byte[] salt)
    {
        using var deriveBytes = new Rfc2898DeriveBytes(
            deviceId,
            salt,
            100_000, // Increased iteration count for stronger key stretching.
            HashAlgorithmName.SHA256
        );
        // Derive a 64-byte key: 32 for encryption, 32 for authentication (HMAC)
        return deriveBytes.GetBytes(64);
    }

    public static byte[] EncryptAES(string plainText, byte[] key)
    {
        // Split the derived key into an encryption key and an authentication key
        byte[] encKey = new byte[32];
        byte[] authKey = new byte[32];
        Buffer.BlockCopy(key, 0, encKey, 0, 32);
        Buffer.BlockCopy(key, 32, authKey, 0, 32);

        using var aes = Aes.Create();
        aes.Key = encKey;
        aes.GenerateIV();
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();

        // Prepend the IV
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
            sw.Flush();
            cs.FlushFinalBlock();
        }

        byte[] encryptedData = ms.ToArray();

        // Append HMAC-SHA256 of the IV + ciphertext
        using var hmac = new HMACSHA256(authKey);
        byte[] hmacHash = hmac.ComputeHash(encryptedData);

        var combinedData = new byte[encryptedData.Length + hmacHash.Length];
        Buffer.BlockCopy(encryptedData, 0, combinedData, 0, encryptedData.Length);
        Buffer.BlockCopy(hmacHash, 0, combinedData, encryptedData.Length, hmacHash.Length);

        return combinedData;
    }

    public static string DecryptAES(byte[] data, byte[] key)
    {
        // Split the derived key into an encryption key and an authentication key
        byte[] encKey = new byte[32];
        byte[] authKey = new byte[32];
        Buffer.BlockCopy(key, 0, encKey, 0, 32);
        Buffer.BlockCopy(key, 32, authKey, 0, 32);

        const int hmacSize = 32; // HMAC-SHA256
        const int ivSize = 16; // AES IV

        if (data == null || data.Length < hmacSize + ivSize)
            throw new CryptographicException("Invalid data: too short to contain IV and HMAC.");

        // 1. Verify HMAC
        byte[] receivedHmac = new byte[hmacSize];
        Array.Copy(data, data.Length - hmacSize, receivedHmac, 0, hmacSize);

        byte[] dataToVerify = new byte[data.Length - hmacSize];
        Array.Copy(data, 0, dataToVerify, 0, dataToVerify.Length);

        using (var hmac = new HMACSHA256(authKey))
        {
            byte[] computedHmac = hmac.ComputeHash(dataToVerify);
            if (!FixedTimeEquals(computedHmac, receivedHmac))
                throw new CryptographicException(
                    "HMAC validation failed. Data is corrupt or key is incorrect."
                );
        }

        // 2. Decrypt if HMAC is valid
        byte[] iv = new byte[ivSize];
        Array.Copy(dataToVerify, 0, iv, 0, iv.Length);

        using var aes = Aes.Create();
        aes.Key = encKey;
        aes.Padding = PaddingMode.PKCS7;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(dataToVerify, iv.Length, dataToVerify.Length - iv.Length);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        try
        {
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }
        catch (CryptographicException ex)
        {
            Debug.LogError(
                $"Cryptographic error during decryption. This often means the key is incorrect or the data is corrupt. Details: {ex.Message}"
            );
            throw; // Re-throw the exception to be handled by the caller
        }
    }

    /// <summary>
    /// Performs a constant-time comparison of two byte arrays to prevent timing attacks.
    /// </summary>
    /// <param name="a">The first byte array.</param>
    /// <param name="b">The second byte array.</param>
    /// <returns>True if the arrays are equal, false otherwise.</returns>
    private static bool FixedTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        int diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];

        return diff == 0;
    }
}
