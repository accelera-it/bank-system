namespace BankSystem.Services.UnitTests.Tests
{
    using System;
    using BankSystem.Tests.Shared;
    using Common.Utils;
    using Common.Utils.Models;
    using FluentAssertions;
    using Newtonsoft.Json;
    using Xunit;

    public class SignatureVerificationUtilTests
    {
        private static readonly (string PublicKey, string PrivateKey) Sender = SecureRequestBuilder.GenerateKeyPair();
        private static readonly (string PublicKey, string PrivateKey) Recipient = SecureRequestBuilder.GenerateKeyPair();

        private static SignatureVerificationModel Model(SecurePayload payload, string signaturePublicKey = null)
            => new SignatureVerificationModel
            {
                DecryptionPrivateKey = Recipient.PrivateKey,
                SignaturePublicKey = signaturePublicKey ?? Sender.PublicKey,
                EncryptedKey = payload.EncryptedKey,
                EncryptedIv = payload.EncryptedIv,
                Data = payload.Data,
                Signature = payload.Signature
            };

        [Fact]
        public void Decrypt_WithValidPayload_ShouldReturnOriginalModel()
        {
            var payload = SecureRequestBuilder.CreatePayload(new { Amount = 5 }, Sender.PrivateKey, Recipient.PublicKey);

            var result = SignatureVerificationUtil.DecryptDataAndVerifySignature(Model(payload));

            result.Should().Be(JsonConvert.SerializeObject(new { Amount = 5 }));
        }

        [Fact]
        public void Decrypt_SignedByAnotherKey_ShouldReturnNull()
        {
            var other = SecureRequestBuilder.GenerateKeyPair();
            var payload = SecureRequestBuilder.CreatePayload(new { Amount = 5 }, other.PrivateKey, Recipient.PublicKey);

            SignatureVerificationUtil.DecryptDataAndVerifySignature(Model(payload)).Should().BeNull();
        }

        [Theory]
        [InlineData(-3)]
        [InlineData(3)]
        public void Decrypt_WithTimestampOutsideWindow_ShouldReturnNull(int minutes)
        {
            var payload = SecureRequestBuilder.CreatePayload(
                new { Amount = 5 }, Sender.PrivateKey, Recipient.PublicKey, DateTime.UtcNow.AddMinutes(minutes));

            SignatureVerificationUtil.DecryptDataAndVerifySignature(Model(payload)).Should().BeNull();
        }

        [Fact]
        public void Decrypt_WithMissingFields_ShouldReturnNull()
            => SignatureVerificationUtil.DecryptDataAndVerifySignature(new SignatureVerificationModel())
                .Should().BeNull();
    }
}
