using System;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace NitroNetwork.Core
{
    /// <summary>
    /// Provides RSA cryptography utilities for key generation, encryption, and decryption.
    /// Includes methods to generate RSA key pairs, convert keys to and from XML, and perform encryption/decryption.
    /// </summary>
    public static class NitroCriptografyRSA
    {
        private const int KeySize = 2048;

        /// <summary>
        /// Generates a new RSA key pair and returns the public and private keys as XML strings.
        /// </summary>
        public static void GenerateKeys(out string publicKeyXml, out string privateKeyXml)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.KeySize = KeySize;
                var parameters = rsa.ExportParameters(true);

                publicKeyXml = ToXmlString(parameters, false);
                privateKeyXml = ToXmlString(parameters, true);
            }
        }

        /// <summary>
        /// Encrypts the given data using the provided RSA public key (in XML format).
        /// </summary>
        public static byte[] Encrypt(string publicKeyXml, byte[] data)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportParameters(FromXmlString(publicKeyXml));
                return rsa.Encrypt(data, RSAEncryptionPadding.Pkcs1); // Compatible with most platforms
            }
        }

        /// <summary>
        /// Decrypts the given encrypted data using the provided RSA private key (in XML format).
        /// </summary>
        public static byte[] Decrypt(string privateKeyXml, byte[] encryptedData)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportParameters(FromXmlString(privateKeyXml));
                return rsa.Decrypt(encryptedData, RSAEncryptionPadding.Pkcs1);
            }
        }

        /// <summary>
        /// Converts RSA parameters to an XML string. Optionally includes private parameters.
        /// </summary>
        private static string ToXmlString(RSAParameters parameters, bool includePrivate)
        {
            XElement root = new XElement("RSAKeyValue",
                new XElement("Modulus", Convert.ToBase64String(parameters.Modulus)),
                new XElement("Exponent", Convert.ToBase64String(parameters.Exponent))
            );

            if (includePrivate)
            {
                root.Add(
                    new XElement("P", Convert.ToBase64String(parameters.P)),
                    new XElement("Q", Convert.ToBase64String(parameters.Q)),
                    new XElement("DP", Convert.ToBase64String(parameters.DP)),
                    new XElement("DQ", Convert.ToBase64String(parameters.DQ)),
                    new XElement("InverseQ", Convert.ToBase64String(parameters.InverseQ)),
                    new XElement("D", Convert.ToBase64String(parameters.D))
                );
            }

            return root.ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>
        /// Parses an XML string to RSA parameters.
        /// </summary>
        private static RSAParameters FromXmlString(string xml)
        {
            var root = XElement.Parse(xml);
            var parameters = new RSAParameters
            {
                Modulus = Convert.FromBase64String(root.Element("Modulus")?.Value ?? ""),
                Exponent = Convert.FromBase64String(root.Element("Exponent")?.Value ?? "")
            };

            if (root.Element("P") != null)
            {
                parameters.P = Convert.FromBase64String(root.Element("P")?.Value ?? "");
                parameters.Q = Convert.FromBase64String(root.Element("Q")?.Value ?? "");
                parameters.DP = Convert.FromBase64String(root.Element("DP")?.Value ?? "");
                parameters.DQ = Convert.FromBase64String(root.Element("DQ")?.Value ?? "");
                parameters.InverseQ = Convert.FromBase64String(root.Element("InverseQ")?.Value ?? "");
                parameters.D = Convert.FromBase64String(root.Element("D")?.Value ?? "");
            }

            return parameters;
        }
    }

    /// <summary>
    /// Provides AES cryptography utilities for key generation, encryption, and decryption.
    /// Includes methods to generate secure AES keys, encrypt data with a random IV, and decrypt data.
    /// </summary>
    public static class NitroCriptografyAES
    {
        private const int KeySizeBits = 256;
        private const int BlockSizeBits = 128;

        /// <summary>
        /// Generates a secure random AES key.
        /// </summary>
        public static byte[] GenerateKeys()
        {
            using var aes = Aes.Create();
            aes.KeySize = KeySizeBits;
            aes.GenerateKey();
            return aes.Key;
        }

        /// <summary>
        /// Encrypts data using AES CBC mode with PKCS7 padding and a random IV.
        /// Returns the encrypted data and IV.
        /// </summary>
        public static AesResult Encrypt(byte[] data, byte[] key)
        {
            using var aes = Aes.Create();
            aes.KeySize = KeySizeBits;
            aes.BlockSize = BlockSizeBits;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.GenerateIV(); // New IV for each message

            using var encryptor = aes.CreateEncryptor();
            byte[] encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);

            return new AesResult
            {
                EncryptedData = encrypted,
                IV = aes.IV
            };
        }

        /// <summary>
        /// Decrypts AES-encrypted data using the provided key and IV.
        /// </summary>
        public static ReadOnlySpan<byte> Decrypt(byte[] encryptedData, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.KeySize = KeySizeBits;
            aes.BlockSize = BlockSizeBits;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            ReadOnlySpan<byte> decryptedBytes = decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
            return decryptedBytes;
        }
    }
}
