namespace BankSystem.IntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Security.Claims;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authentication.Cookies;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using Web.Infrastructure.Kyc;
    using Xunit;

    public class KycTokenRefresherTests : IClassFixture<BankWebFactory>
    {
        private const string Tokens =
            "{\"id_token\":\"new-id\",\"refresh_token\":\"new-refresh\",\"expires_in\":300}";

        private readonly BankWebFactory factory;

        public KycTokenRefresherTests(BankWebFactory factory) => this.factory = factory;

        private sealed class Keycloak : HttpMessageHandler
        {
            private readonly Func<HttpResponseMessage> answer;

            public Keycloak(Func<HttpResponseMessage> answer) => this.answer = answer;

            public int Calls { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                this.Calls++;
                return Task.FromResult(this.answer());
            }
        }

        private async Task<CookieValidatePrincipalContext> RunAsync(Keycloak keycloak, DateTimeOffset expiresAt)
        {
            using var scope = this.factory.Services.CreateScope();
            var services = scope.ServiceProvider;
            var properties = new AuthenticationProperties();
            properties.StoreTokens(new[]
            {
                new AuthenticationToken { Name = KycTokenRefresher.IdToken, Value = "old-id" },
                new AuthenticationToken { Name = KycTokenRefresher.RefreshToken, Value = "old-refresh" },
                new AuthenticationToken { Name = KycTokenRefresher.ExpiresAt, Value = expiresAt.ToString("o") }
            });

            var scheme = new AuthenticationScheme(
                IdentityConstants.ApplicationScheme, null, typeof(CookieAuthenticationHandler));
            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity("test")), properties, scheme.Name);
            var http = new DefaultHttpContext { RequestServices = services };
            var context = new CookieValidatePrincipalContext(http, scheme, new CookieAuthenticationOptions(), ticket);

            var refresher = new KycTokenRefresher(
                new HttpClient(keycloak),
                services.GetRequiredService<IOptions<BankSystem.Common.Configuration.KycConfiguration>>(),
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["BANK_CLIENT_SECRET"] = "secret"
                }).Build());

            await refresher.RefreshIfExpiredAsync(context);

            return context;
        }

        [Fact]
        public async Task LiveTokens_ShouldNotCallKeycloak()
        {
            var keycloak = new Keycloak(() => throw new InvalidOperationException());

            var context = await this.RunAsync(keycloak, DateTimeOffset.UtcNow.AddMinutes(1));

            keycloak.Calls.Should().Be(0);
            context.Principal.Should().NotBeNull();
        }

        [Fact]
        public async Task ExpiredTokens_ShouldBeReplaced()
        {
            var keycloak = new Keycloak(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Tokens)
            });

            var context = await this.RunAsync(keycloak, DateTimeOffset.UtcNow.AddMinutes(-1));

            context.Properties.GetTokenValue(KycTokenRefresher.RefreshToken).Should().Be("new-refresh");
            context.Properties.GetTokenValue(KycTokenRefresher.IdToken).Should().Be("new-id");
            context.ShouldRenew.Should().BeTrue();
            context.Principal.Should().NotBeNull();
        }

        [Fact]
        public async Task RefusedRefresh_ShouldEndTheSession()
        {
            var keycloak = new Keycloak(() => new HttpResponseMessage(HttpStatusCode.BadRequest));

            var context = await this.RunAsync(keycloak, DateTimeOffset.UtcNow.AddMinutes(-1));

            context.Principal.Should().BeNull();
        }

        [Fact]
        public async Task UnreachableKeycloak_ShouldKeepTheSession()
        {
            var keycloak = new Keycloak(() => throw new HttpRequestException("away"));

            var context = await this.RunAsync(keycloak, DateTimeOffset.UtcNow.AddMinutes(-1));

            context.Principal.Should().NotBeNull();
            context.Properties.GetTokenValue(KycTokenRefresher.RefreshToken).Should().Be("old-refresh");
        }
    }
}
