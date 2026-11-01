namespace BankSystem.Web.Infrastructure.Kyc
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Common.Configuration;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authentication.Cookies;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Options;

    /// <summary>Renews the Keycloak tokens of a session, and ends the session once Keycloak refuses.</summary>
    public class KycTokenRefresher : IKycTokenRefresher
    {
        public const string IdToken = "id_token";
        public const string RefreshToken = "refresh_token";
        public const string ExpiresAt = "expires_at";

        private const string ClientSecretKey = "BANK_CLIENT_SECRET";

        private readonly HttpClient client;
        private readonly KycConfiguration kyc;
        private readonly string clientSecret;

        public KycTokenRefresher(HttpClient client, IOptions<KycConfiguration> kyc, IConfiguration configuration)
        {
            this.client = client;
            this.kyc = kyc.Value;
            this.clientSecret = configuration[ClientSecretKey];
        }

        public async Task RefreshIfExpiredAsync(CookieValidatePrincipalContext context)
        {
            var refreshToken = context.Properties.GetTokenValue(RefreshToken);
            var expiresAt = context.Properties.GetTokenValue(ExpiresAt);
            if (string.IsNullOrEmpty(refreshToken) ||
                !DateTimeOffset.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expires) ||
                expires > DateTimeOffset.UtcNow)
            {
                return;
            }

            HttpResponseMessage response;
            try
            {
                response = await this.client.SendAsync(this.TokenRequest(refreshToken));
            }
            catch (HttpRequestException)
            {
                // Keycloak being away is no reason to sign anyone out; the next request tries again
                return;
            }

            using (response)
            {
                if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                    return;
                }

                if (response.IsSuccessStatusCode)
                {
                    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    Store(context, json.RootElement);
                }
            }
        }

        private static void Store(CookieValidatePrincipalContext context, JsonElement tokens)
        {
            var expires = DateTimeOffset.UtcNow.AddSeconds(tokens.GetProperty("expires_in").GetInt32());

            context.Properties.UpdateTokenValue(IdToken, tokens.GetProperty(IdToken).GetString());
            context.Properties.UpdateTokenValue(RefreshToken, tokens.GetProperty(RefreshToken).GetString());
            context.Properties.UpdateTokenValue(ExpiresAt, expires.ToString("o", CultureInfo.InvariantCulture));
            context.ShouldRenew = true;
        }

        private HttpRequestMessage TokenRequest(string refreshToken)
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{this.kyc.ClientId}:{this.clientSecret}"));

            return new HttpRequestMessage(HttpMethod.Post, $"{this.kyc.Authority}/protocol/openid-connect/token")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Basic", credentials) },
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    [RefreshToken] = refreshToken
                })
            };
        }
    }
}
