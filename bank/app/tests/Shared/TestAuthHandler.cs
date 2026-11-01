namespace BankSystem.Tests.Shared
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Security.Claims;
    using System.Text.Encodings.Web;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Signs a request in from test headers. Requests without them stay anonymous,
    /// so the app's own login redirects keep working.
    /// </summary>
    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        private const string UserNameHeader = "X-Test-User";
        private const string UserIdHeader = "X-Test-UserId";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        public static void SignIn(HttpClient client, string userName, string userId)
        {
            client.DefaultRequestHeaders.Add(UserNameHeader, userName);
            client.DefaultRequestHeaders.Add(UserIdHeader, userId);
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.TryGetValue(UserNameHeader, out var userName))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, userName.ToString()),
                new Claim(ClaimTypes.NameIdentifier, this.Request.Headers[UserIdHeader].ToString())
            };

            // Identity's authentication type, so SignInManager.IsSignedIn is true in views
            var identity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    public static class TestAuthExtensions
    {
        public static IServiceCollection AddTestAuthentication(this IServiceCollection services)
        {
            services
                .AddAuthentication(options => options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);

            return services;
        }
    }
}
