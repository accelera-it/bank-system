namespace BankSystem.Tests.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;
    using Common.Utils;
    using Newtonsoft.Json;

    /// <summary>
    /// Builds requests the same way banks and CentralApi do:
    /// sign with the sender's private key, encrypt with AES and the recipient's public key.
    /// </summary>
    public static class SecureRequestBuilder
    {
        public static (string PublicKey, string PrivateKey) GenerateKeyPair(int keySize = 2048)
        {
            using var rsa = RSA.Create(keySize);

            return (rsa.ToXmlString(false), rsa.ToXmlString(true));
        }

        public static SecurePayload CreatePayload(
            object model,
            string signingPrivateKey,
            string recipientPublicKey,
            DateTime? timestamp = null)
        {
            var data = JsonConvert.SerializeObject(new
            {
                Model = JsonConvert.SerializeObject(model),
                Timestamp = timestamp ?? DateTime.UtcNow
            });

            using var signingRsa = RSA.Create();
            signingRsa.FromXmlString(signingPrivateKey);
            var signature = signingRsa.SignData(
                Encoding.UTF8.GetBytes(data), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            var aesParams = CryptographyExtensions.GenerateKey();
            var key = Convert.FromBase64String(aesParams[0]);
            var iv = Convert.FromBase64String(aesParams[1]);

            using var encryptionRsa = RSA.Create();
            encryptionRsa.FromXmlString(recipientPublicKey);

            return new SecurePayload
            {
                EncryptedKey = Convert.ToBase64String(encryptionRsa.Encrypt(key, RSAEncryptionPadding.Pkcs1)),
                EncryptedIv = Convert.ToBase64String(encryptionRsa.Encrypt(iv, RSAEncryptionPadding.Pkcs1)),
                Data = Convert.ToBase64String(CryptographyExtensions.Encrypt(data, key, iv)),
                Signature = Convert.ToBase64String(signature)
            };
        }

        /// <summary>
        /// Serializes a payload into the Base64 request body, optionally with extra fields
        /// (e.g. BankName, BankSwiftCode, BankCountry sent by banks to CentralApi).
        /// </summary>
        public static string ToRequestBody(SecurePayload payload, IDictionary<string, object> extraFields = null)
        {
            var body = new Dictionary<string, object>(extraFields ?? new Dictionary<string, object>())
            {
                [nameof(SecurePayload.EncryptedKey)] = payload.EncryptedKey,
                [nameof(SecurePayload.EncryptedIv)] = payload.EncryptedIv,
                [nameof(SecurePayload.Data)] = payload.Data,
                [nameof(SecurePayload.Signature)] = payload.Signature
            };

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body)));
        }
    }

    public class SecurePayload
    {
        public string EncryptedKey { get; set; }

        public string EncryptedIv { get; set; }

        public string Data { get; set; }

        public string Signature { get; set; }
    }
}
