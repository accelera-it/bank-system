namespace BankSystem.IntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using BankSystem.Models;
    using BankSystem.Tests.Shared;
    using Common;
    using FluentAssertions;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Web.Infrastructure.Kyc;
    using Xunit;

    public class AuthPagesTests : IClassFixture<BankWebFactory>
    {
        private readonly BankWebFactory factory;

        public AuthPagesTests(BankWebFactory factory) => this.factory = factory;

        private HttpClient Client()
            => this.factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        [Fact]
        public async Task Login_ShouldGoToKeycloakAndComeBackToTheCallback()
        {
            var response = await this.Client().GetAsync("/account/login?returnUrl=%2Fpay");

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            var location = Uri.UnescapeDataString(response.Headers.Location!.ToString());
            location.Should().StartWith("http://kyc.test/realms/abs/auth")
                .And.Contain("client_id=bank")
                .And.Contain("/signin-oidc")
                .And.NotContain("prompt=");
        }

        [Fact]
        public async Task Register_ShouldOpenKeycloakRegistration()
        {
            var response = await this.Client().GetAsync("/account/register");

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should()
                .StartWith("http://kyc.test/realms/abs/auth")
                .And.Contain("prompt=create");
        }

        [Fact]
        public async Task Callback_WithoutKeycloakLogin_ShouldGoHome()
        {
            var response = await this.Client().GetAsync("/account/callback");

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should().Be("/");
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
            var response = await this.Client().PostAsync("/payments/pay", new FormUrlEncodedContent(new Dictionary<string, string>()));

            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task KycUser_FirstLogin_ShouldCreateTheBankUser()
        {
            using var scope = this.factory.Services.CreateScope();
            var kycUsers = scope.ServiceProvider.GetRequiredService<IKycUserService>();

            var user = await kycUsers.FindOrCreateAsync("new.client@abs.uz", "New Client");

            user.FullName.Should().Be("New Client");
            user.EmailConfirmed.Should().BeTrue();
            user.PasswordHash.Should().BeNull();
        }

        [Fact]
        public async Task KycUser_KnownEmail_ShouldTakeTheNameFromKeycloak()
        {
            using var scope = this.factory.Services.CreateScope();
            var kycUsers = scope.ServiceProvider.GetRequiredService<IKycUserService>();
            await kycUsers.FindOrCreateAsync("renamed@abs.uz", "Old Name");

            var user = await kycUsers.FindOrCreateAsync("renamed@abs.uz", "New Name");

            user.FullName.Should().Be("New Name");
        }

        [Fact]
        public async Task Menu_ShouldLinkToKeycloakProfileAndPassword()
        {
            var account = await this.factory.GetSeededAccountAsync();
            var client = this.Client();
            TestAuthHandler.SignIn(client, BankWebFactory.SeededUserName, account.UserId);

            var html = await client.GetStringAsync("/");

            html.Should().Contain("href=\"/account/profile?returnUrl=%2Fupdated.html\"")
                .And.Contain("href=\"/account/password?returnUrl=%2Fupdated.html\"")
                .And.Contain("Edit profile")
                .And.Contain("Change password")
                .And.Contain(BankWebFactory.SeededUserFullName)
                .And.Contain("btn btn-danger btn-block")
                .And.NotContain("app-user-email");
        }

        [Theory]
        [InlineData("/account/profile", "kc_action=UPDATE_PROFILE")]
        [InlineData("/account/password", "kc_action=UPDATE_PASSWORD")]
        public async Task KeycloakForms_ShouldComeBackThroughTheCallback(string url, string action)
        {
            var account = await this.factory.GetSeededAccountAsync();
            var client = this.Client();
            TestAuthHandler.SignIn(client, BankWebFactory.SeededUserName, account.UserId);

            var response = await client.GetAsync(url);

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should()
                .StartWith("http://kyc.test/realms/abs/auth")
                .And.Contain(action);
        }

        [Fact]
        public async Task OldSettingsPage_ShouldBeGone()
        {
            var response = await this.Client().GetAsync("/account/settings/index");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task SeededUser_ShouldBeAdministrator()
        {
            using var scope = this.factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<BankUser>>();

            var user = await users.FindByNameAsync(BankWebFactory.SeededUserName);

            user.EmailConfirmed.Should().BeTrue();
            (await users.IsInRoleAsync(user, GlobalConstants.AdministratorRoleName)).Should().BeTrue();
        }
    }
}
