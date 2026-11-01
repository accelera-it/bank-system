namespace BankSystem.IntegrationTests
{
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
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
        [InlineData("/account/register", "Create account")]
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
        public async Task PaymentConfirmation_ShouldBeRoutedWhereTheScriptPosts()
        {
            var client = this.factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var response = await client.PostAsync("/payments/pay", new FormUrlEncodedContent(new Dictionary<string, string>()));

            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Register_ShouldStartWithButtonDisabled()
        {
            var html = await this.factory.CreateClient().GetStringAsync("/account/register");

            html.Should().Contain("name=\"Input.AcceptAgreement\"")
                .And.MatchRegex("<button id=\"RegisterButton\"[^>]* disabled");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Register_ShouldNeedTheAgreement(bool accepted)
        {
            var email = $"agreement-{accepted}@abs.uz".ToLowerInvariant();
            var client = this.factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var page = await client.GetStringAsync("/account/register");
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Input.Email"] = email,
                ["Input.FullName"] = "Agreement Test",
                ["Input.Password"] = "Passw0rd",
                ["Input.ConfirmPassword"] = "Passw0rd",
            };
            if (accepted)
            {
                form["Input.AcceptAgreement"] = "true";
            }

            var response = await client.PostAsync("/account/register", new FormUrlEncodedContent(form));

            using var scope = this.factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<BankUser>>();
            (await users.FindByEmailAsync(email) != null).Should().Be(accepted);
            response.StatusCode.Should().Be(accepted ? HttpStatusCode.Redirect : HttpStatusCode.OK);
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
