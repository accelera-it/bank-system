namespace BankSystem.IntegrationTests
{
    using System.Net;
    using System.Threading.Tasks;
    using BankSystem.Models;
    using Common;
    using FluentAssertions;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Xunit;

    public class AuthPopupTests : IClassFixture<BankWebFactory>
    {
        private readonly BankWebFactory factory;

        public AuthPopupTests(BankWebFactory factory) => this.factory = factory;

        [Theory]
        [InlineData("/account/login", "auth=login")]
        [InlineData("/account/register", "auth=register")]
        public async Task AccountPages_RedirectToLandingPopup(string url, string expectedQuery)
        {
            using var client = this.factory.CreateClient(
                new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var response = await client.GetAsync(url);

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location.ToString().Should().Contain(expectedQuery);
        }

        [Fact]
        public async Task Landing_HasPopupFormsAndNoNavbar()
        {
            using var client = this.factory.CreateClient();

            var html = await client.GetStringAsync("/?auth=login");

            html.Should().Contain("id=\"loginModal\"").And.Contain("id=\"registerModal\"").And.NotContain("navbar-brand");
        }

        [Fact]
        public async Task SeededAdministrator_CanSignInAndIsAdministrator()
        {
            using var scope = this.factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<BankUser>>();

            var user = await users.FindByNameAsync(BankWebFactory.SeededUserName);

            user.EmailConfirmed.Should().BeTrue();
            (await users.CheckPasswordAsync(user, "123456")).Should().BeTrue();
            (await users.IsInRoleAsync(user, GlobalConstants.AdministratorRoleName)).Should().BeTrue();
        }
    }
}
