namespace BankSystem.IntegrationTests
{
    using System.Net;
    using System.Threading.Tasks;
    using BankSystem.Tests.Shared;
    using FluentAssertions;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Xunit;

    public class PagesTests : IClassFixture<BankWebFactory>
    {
        private readonly BankWebFactory factory;

        public PagesTests(BankWebFactory factory) => this.factory = factory;

        private System.Net.Http.HttpClient Client()
            => this.factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        [Theory]
        [InlineData("/")]
        public async Task AnonymousPages_ShouldRender(string url)
        {
            var response = await this.Client().GetAsync(url);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Theory]
        [InlineData("/bankaccounts/create")]
        [InlineData("/cards/index")]
        [InlineData("/moneytransfers")]
        [InlineData("/administration/transactions/search")]
        public async Task ProtectedPages_WhenAnonymous_ShouldRedirectToLogin(string url)
        {
            var response = await this.Client().GetAsync(url);

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location.ToString().Should().Contain("/account/login");
        }

        [Fact]
        public async Task Home_WhenSignedIn_ShouldShowOwnAccount()
        {
            var account = await this.factory.GetSeededAccountAsync();
            var client = this.Client();
            TestAuthHandler.SignIn(client, BankWebFactory.SeededUserName, account.UserId);

            var response = await client.GetAsync("/");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain(account.Name);
        }

        [Fact]
        public async Task NewCard_WhenRequestedByThePopup_ShouldReturnOnlyTheForm()
        {
            var account = await this.factory.GetSeededAccountAsync();
            var client = this.Client();
            TestAuthHandler.SignIn(client, BankWebFactory.SeededUserName, account.UserId);
            client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

            var response = await client.GetAsync("/cards/create");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain("name=\"AccountId\"")
                .And.Contain(account.Name)
                .And.NotContain("<html");
        }

        [Fact]
        public async Task NewCard_WhenOpenedDirectly_ShouldRedirectToCards()
        {
            var account = await this.factory.GetSeededAccountAsync();
            var client = this.Client();
            TestAuthHandler.SignIn(client, BankWebFactory.SeededUserName, account.UserId);

            var response = await client.GetAsync("/cards/create");

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location.ToString().Should().Contain("/cards/index");
        }

        [Fact]
        public async Task EditCard_WhenCardIsUnknown_ShouldReturnNotFoundToThePopup()
        {
            var account = await this.factory.GetSeededAccountAsync();
            var client = this.Client();
            TestAuthHandler.SignIn(client, BankWebFactory.SeededUserName, account.UserId);
            client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

            var response = await client.GetAsync("/cards/edit/missing");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task AccountDetails_OfAnotherUser_ShouldBeRefused()
        {
            var account = await this.factory.GetSeededAccountAsync();
            var client = this.Client();
            TestAuthHandler.SignIn(client, "stranger@test.com", "stranger-id");

            var response = await client.GetAsync($"/bankaccounts/details/{account.Id}");

            response.StatusCode.Should().NotBe(HttpStatusCode.OK);
        }
    }
}
