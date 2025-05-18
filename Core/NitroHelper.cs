using System;
using System.Security.Cryptography;
using System.Text;
using NitroNetwork.Core;

public static class NitroCriptografy
{
    private const int KeySize = 2048;

    // Gera chaves em formato XML (compatível com Unity em todas as plataformas)
    public static void GenerateKeys(out string publicKeyXml, out string privateKeyXml)
    {
        using (var rsa = new RSACryptoServiceProvider(KeySize))
        {
            publicKeyXml = rsa.ToXmlString(false);  // somente pública
            privateKeyXml = rsa.ToXmlString(true);  // pública + privada
        }
    }

    public static byte[] Encrypt(string publicKeyXml, byte[] data)
    {
        using (var rsa = new RSACryptoServiceProvider(KeySize))
        {
            rsa.FromXmlString(publicKeyXml);

            return rsa.Encrypt(data, false);  // false = PKCS#1 v1.5 padding (compatível)
        }
    }

    public static ReadOnlySpan<byte> Decrypt(string privateKeyXml, byte[] encryptedData)
    {
        using (var rsa = new RSACryptoServiceProvider(KeySize))
        {
            rsa.FromXmlString(privateKeyXml);
            ReadOnlySpan<byte> decrypted = rsa.Decrypt(encryptedData, false);
            
            return decrypted;
        }
    }
}
