namespace DemoShop.UnitTests
{
    using System;
    using System.Text;
    using BankSystem.Tests.Shared;
    using FluentAssertions;
    using Newtonsoft.Json.Linq;
    using Web.PaymentHelpers;
    using Xunit;

    public class DirectPaymentsHelperTests
    {
        private readonly DemoPaymentChain chain = new DemoPaymentChain();

        private object Process(string data)
            => DirectPaymentsHelper.ProcessPaymentResult(data, this.chain.Site.Private, this.chain.CentralApi.Public);

        [Fact]
        public void GeneratePaymentRequest_ShouldEmbedInfoSiteKeyAndReturnUrl()
        {
            var encoded = DirectPaymentsHelper.GeneratePaymentRequest(
                new { Amount = 5 }, this.chain.Site.Private, "https://shop.test/return");

            var request = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
            request["ReturnUrl"].ToString().Should().Be("https://shop.test/return");
            request["PaymentInfo"].ToString().Should().Contain("Amount");
            request["PublicKey"].ToString().Should().NotContain("<D>");
        }

        [Fact]
        public void ProcessPaymentResult_WithValidChain_ShouldReturnPaymentInfo()
        {
            dynamic result = this.Process(this.chain.CreateResult("order-1"));

            ((string)result.OrderId).Should().Be("order-1");
        }

        [Fact]
        public void ProcessPaymentResult_WithForgedMerchantSignature_ShouldReturnNull()
            => this.Process(this.chain.CreateResult("order-1", paymentInfoSignedWith: new DemoPaymentChain().Site.Private))
                .Should().BeNull();

        [Fact]
        public void ProcessPaymentResult_WithUntrustedCentralApi_ShouldReturnNull()
            => this.Process(new DemoPaymentChain().CreateResult("order-1")).Should().BeNull();

        [Fact]
        public void ProcessPaymentResult_WithFailedPayment_ShouldReturnNull()
            => this.Process(this.chain.CreateResult("order-1", success: false)).Should().BeNull();

        [Fact]
        public void ProcessPaymentResult_WithGarbage_ShouldReturnNull()
            => this.Process("not-base64").Should().BeNull();
    }
}
