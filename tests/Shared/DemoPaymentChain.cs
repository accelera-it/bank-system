namespace BankSystem.Tests.Shared
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using Newtonsoft.Json;

    /// <summary>
    /// Builds what a merchant receives after a direct payment:
    /// PaymentInfo (merchant key) -> PaymentProof (CentralApi key) -> PaymentConfirmation (bank key).
    /// </summary>
    public class DemoPaymentChain
    {
        public DemoPaymentChain()
        {
            this.Site = NewKeys();
            this.CentralApi = NewKeys();
            this.Bank = NewKeys();
        }

        public (string Public, string Private) Site { get; }

        public (string Public, string Private) CentralApi { get; }

        public (string Public, string Private) Bank { get; }

        public string CreateResult(string orderId, bool success = true, string paymentInfoSignedWith = null)
        {
            var paymentInfo = JsonConvert.SerializeObject(new { OrderId = orderId });
            var paymentProof = JsonConvert.SerializeObject(new
            {
                BankPublicKey = this.Bank.Public,
                PaymentInfoSignature = Sign(paymentInfo, paymentInfoSignedWith ?? this.Site.Private)
            });
            var confirmation = JsonConvert.SerializeObject(new
            {
                PaymentProofSignature = Sign(paymentProof, this.CentralApi.Private),
                Success = success
            });
            var result = JsonConvert.SerializeObject(new
            {
                PaymentInfo = paymentInfo,
                PaymentProof = paymentProof,
                PaymentConfirmation = confirmation,
                PaymentConfirmationSignature = Sign(confirmation, this.Bank.Private)
            });

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(result));
        }

        private static (string Public, string Private) NewKeys()
        {
            using var rsa = RSA.Create(2048);

            return (rsa.ToXmlString(false), rsa.ToXmlString(true));
        }

        private static string Sign(string data, string privateKey)
        {
            using var rsa = RSA.Create();
            rsa.FromXmlString(privateKey);

            return Convert.ToBase64String(rsa.SignData(
                Encoding.UTF8.GetBytes(data), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }
    }
}
