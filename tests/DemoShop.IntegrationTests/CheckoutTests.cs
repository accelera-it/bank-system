namespace DemoShop.IntegrationTests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using BankSystem.Tests.Shared;
    using DemoShop.Models;
    using FluentAssertions;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Xunit;

    public class CheckoutTests : IClassFixture<DemoShopFactory>
    {
        private readonly DemoShopFactory factory;
        private readonly HttpClient anonymous;
        private readonly HttpClient buyer;

        public CheckoutTests(DemoShopFactory factory)
        {
            this.factory = factory;
            var options = new WebApplicationFactoryClientOptions { AllowAutoRedirect = false };
            this.anonymous = factory.CreateClient(options);
            this.buyer = factory.CreateClient(options);
            TestAuthHandler.SignIn(this.buyer, DemoShopFactory.UserName, "buyer");
        }

        private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields)
            => new FormUrlEncodedContent(fields.ToDictionary(f => f.Key, f => f.Value));

        private Task<HttpResponseMessage> PayByCardAsync(string orderId, string expiryDate = "12/30")
            => this.buyer.PostAsync($"/CardPayments/Pay/{orderId}", Form(
                ("Number", "4111111111111111"),
                ("Name", "Test Testov"),
                ("ExpiryDate", expiryDate),
                ("SecurityCode", "123")));

        private Task<HttpResponseMessage> ConfirmAsync(string data)
            => this.anonymous.PostAsync("/DirectPayments/ReceiveConfirmation", Form(("data", data)));

        [Fact]
        public async Task Home_ShouldListProducts()
        {
            await this.factory.EnsureSeedAsync();

            var html = await this.anonymous.GetStringAsync("/");

            html.Should().Contain(DemoShopFactory.ProductName);
        }

        [Fact]
        public async Task MyOrders_WhenAnonymous_ShouldRedirectToLogin()
        {
            var response = await this.anonymous.GetAsync("/Orders/My");

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location.ToString().Should().Contain("Login");
        }

        [Fact]
        public async Task OrderProduct_ByCard_ShouldCreateOrderAndRedirectToPayment()
        {
            var (_, productId) = await this.factory.EnsureSeedAsync();

            var response = await this.buyer.PostAsync("/Orders/OrderProduct", Form(
                ("productId", productId), ("paymentMethod", "Card")));

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            var orderId = response.Headers.Location.ToString().Split('/').Last();
            (await this.factory.GetOrderStatusAsync(orderId)).Should().Be(PaymentStatus.Pending);
            (await this.buyer.GetAsync(response.Headers.Location)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task CardPayment_WhenCentralApiAccepts_ShouldCompleteOrder()
        {
            var orderId = await this.factory.CreateOrderAsync();
            this.factory.CentralApi.ResponseStatus = HttpStatusCode.OK;

            var response = await this.PayByCardAsync(orderId);

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            (await this.factory.GetOrderStatusAsync(orderId)).Should().Be(PaymentStatus.Completed);
        }

        [Fact]
        public async Task CardPayment_WhenCentralApiRefuses_ShouldKeepOrderPending()
        {
            var orderId = await this.factory.CreateOrderAsync();
            this.factory.CentralApi.ResponseStatus = HttpStatusCode.BadRequest;

            try
            {
                var response = await this.PayByCardAsync(orderId);

                response.StatusCode.Should().Be(HttpStatusCode.OK);
                (await this.factory.GetOrderStatusAsync(orderId)).Should().Be(PaymentStatus.Pending);
            }
            finally
            {
                this.factory.CentralApi.ResponseStatus = HttpStatusCode.OK;
            }
        }

        [Theory]
        [InlineData("13/30")]
        [InlineData("1a/30")]
        public async Task CardPayment_WithInvalidExpiryDate_ShouldNotContactCentralApi(string expiryDate)
        {
            var orderId = await this.factory.CreateOrderAsync();
            var requestsBefore = this.factory.CentralApi.Requests.Count;

            var response = await this.PayByCardAsync(orderId, expiryDate);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            this.factory.CentralApi.Requests.Count.Should().Be(requestsBefore);
            (await this.factory.GetOrderStatusAsync(orderId)).Should().Be(PaymentStatus.Pending);
        }

        [Fact]
        public async Task DirectPayment_ShouldPostSignedRequestToCentralApi()
        {
            var orderId = await this.factory.CreateOrderAsync();

            var html = await this.buyer.GetStringAsync($"/DirectPayments/Pay/{orderId}");

            html.Should().Contain(this.factory.CentralApi.Url + "pay");
        }

        [Fact]
        public async Task ReceiveConfirmation_WithValidChain_ShouldCompleteOrder()
        {
            var orderId = await this.factory.CreateOrderAsync();

            var response = await this.ConfirmAsync(this.factory.Chain.CreateResult(orderId));

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            (await this.factory.GetOrderStatusAsync(orderId)).Should().Be(PaymentStatus.Completed);
        }

        [Fact]
        public async Task ReceiveConfirmation_WithForgedPaymentInfo_ShouldReturnBadRequest()
        {
            var orderId = await this.factory.CreateOrderAsync();
            var forged = this.factory.Chain.CreateResult(orderId, paymentInfoSignedWith: new DemoPaymentChain().Site.Private);

            var response = await this.ConfirmAsync(forged);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await this.factory.GetOrderStatusAsync(orderId)).Should().Be(PaymentStatus.Pending);
        }

        [Fact]
        public async Task ReceiveConfirmation_WithFailedPayment_ShouldReturnBadRequest()
        {
            var orderId = await this.factory.CreateOrderAsync();

            var response = await this.ConfirmAsync(this.factory.Chain.CreateResult(orderId, success: false));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await this.factory.GetOrderStatusAsync(orderId)).Should().Be(PaymentStatus.Pending);
        }

        [Fact]
        public async Task ReceiveConfirmation_WithoutData_ShouldReturnBadRequest()
        {
            var response = await this.anonymous.PostAsync(
                "/DirectPayments/ReceiveConfirmation", new FormUrlEncodedContent(new Dictionary<string, string>()));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
