using System;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
namespace NitroNetwork.Core
{
    public static class NitroCriptografyRSA
    {
        private const int KeySize = 2048;

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

        public static byte[] Encrypt(string publicKeyXml, byte[] data)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportParameters(FromXmlString(publicKeyXml));
                return rsa.Encrypt(data, RSAEncryptionPadding.Pkcs1); // compatível
            }
        }

        public static byte[] Decrypt(string privateKeyXml, byte[] encryptedData)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportParameters(FromXmlString(privateKeyXml));
                return rsa.Decrypt(encryptedData, RSAEncryptionPadding.Pkcs1);
            }
        }

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
    public static class NitroCriptografyAES
    {

        private const int KeySizeBits = 256;
        private const int BlockSizeBits = 128;

       
        // Gera uma chave AES segura em Base64
        public static byte[] GenerateKeys()
        {
            using var aes = Aes.Create();
            aes.KeySize = KeySizeBits;
            aes.GenerateKey();
            return aes.Key;
        }

        // Criptografa e retorna EncryptedData + IV
        public static AesResult Encrypt(byte[] data, byte[] key)
        {
            using var aes = Aes.Create();
            aes.KeySize = KeySizeBits;
            aes.BlockSize = BlockSizeBits;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.GenerateIV(); // NOVA IV A CADA MENSAGEM

            using var encryptor = aes.CreateEncryptor();
            byte[] encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);

            return new AesResult
            {
                EncryptedData = encrypted,
                IV = aes.IV
            };
        }

        // Descriptografa com chave e IV
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
