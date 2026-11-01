namespace CentralApi.IntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using BankSystem.Tests.Shared;
    using FluentAssertions;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Newtonsoft.Json;
    using Xunit;

    public class DirectPaymentsTests : IClassFixture<CentralApiFactory>
    {
        private readonly CentralApiFactory factory;
        private readonly HttpClient client;

        public DirectPaymentsTests(CentralApiFactory factory)
        {
            this.factory = factory;
            this.client = factory.CreateClient();
        }

        private static string PaymentRequest(decimal amount = 20)
        {
            var (publicKey, privateKey) = SecureRequestBuilder.GenerateKeyPair();
            var paymentInfo = JsonConvert.SerializeObject(new { Amount = amount, Description = "Direct purchase" });

            using var rsa = RSA.Create();
            rsa.FromXmlString(privateKey);
            var signature = Convert.ToBase64String(rsa.SignData(
                Encoding.UTF8.GetBytes(paymentInfo), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

            var request = JsonConvert.SerializeObject(new
            {
                PaymentInfo = paymentInfo,
                PaymentInfoSignature = signature,
                PublicKey = publicKey,
                ReturnUrl = "https://shop.test/return"
            });

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(request));
        }

        private Task<HttpResponseMessage> PostPaymentAsync(string data)
            => this.client.PostAsync("/pay", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["data"] = data
            }));

        [Fact]
        public async Task Pay_WithSignedRequest_ShouldListBanksAndRedirectToChosenBank()
        {
            var bankId = await this.factory.EnsureTestBankAsync();

            var selection = await this.PostPaymentAsync(PaymentRequest());

            selection.StatusCode.Should().Be(HttpStatusCode.OK);
            (await selection.Content.ReadAsStringAsync()).Should().Contain(CentralApiFactory.BankName);

            var redirect = await this.client.PostAsync("/pay/continue", new FormUrlEncodedContent(
                new Dictionary<string, string> { ["bankId"] = bankId }));

            redirect.StatusCode.Should().Be(HttpStatusCode.OK);
            (await redirect.Content.ReadAsStringAsync()).Should().Contain(this.factory.BankServer.Url + "pay");
        }

        [Fact]
        public async Task Pay_BehindGateway_ShouldKeepForwardedPrefixInRedirect()
        {
            var client = this.factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using var request = new HttpRequestMessage(HttpMethod.Post, "/pay")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["data"] = PaymentRequest() })
            };
            request.Headers.Add("X-Forwarded-Prefix", "/api");

            var response = await client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should().Be("/api/pay");
        }

        [Fact]
        public async Task Pay_WithInvalidEncoding_ShouldReturnBadRequest()
        {
            var response = await this.PostPaymentAsync("%%%");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Pay_WithNonPositiveAmount_ShouldReturnBadRequest()
        {
            var response = await this.PostPaymentAsync(PaymentRequest(amount: 0));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Process_WithoutPaymentCookie_ShouldReturnBadRequest()
        {
            var response = await this.factory.CreateClient().GetAsync("/pay");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
