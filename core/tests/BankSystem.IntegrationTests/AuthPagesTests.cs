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

    public class AuthPagesTests : IClassFixture<BankWebFactory>
    {
        private readonly BankWebFactory factory;

        public AuthPagesTests(BankWebFactory factory) => this.factory = factory;

        [Theory]
        [InlineData("/account/login", "Log in")]
        [InlineData("/account/register", "Open an account")]
        public async Task AccountPages_ShouldRenderTheirOwnForm(string url, string title)
        {
            using var client = this.factory.CreateClient();

            var response = await client.GetAsync(url);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var html = await response.Content.ReadAsStringAsync();
            html.Should().Contain($"<title>{title} - Bank System</title>")
                .And.Contain("name=\"Input.Password\"")
                .And.NotContain("class=\"modal");
        }

        [Fact]
        public async Task Landing_LinksToAccountPagesWithoutPopups()
        {
            using var client = this.factory.CreateClient();

            var html = await client.GetStringAsync("/");

            html.Should().Contain("href=\"/account/login\"")
                .And.Contain("href=\"/account/register\"")
                .And.NotContain("loginModal")
                .And.NotContain("registerModal");
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
