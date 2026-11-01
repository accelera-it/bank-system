namespace BankSystem.Web.Infrastructure.Kyc
{
    using System.Collections.Generic;
    using Common.Configuration;
    using Microsoft.AspNetCore.WebUtilities;

    public static class KycUrls
    {
        /// <summary>Ends the Keycloak session, which then sends the browser to the given address.</summary>
        public static string Logout(KycConfiguration kyc, string idToken, string redirectUri)
            => QueryHelpers.AddQueryString(
                $"{kyc.PublicAuthority}/protocol/openid-connect/logout",
                new Dictionary<string, string>
                {
                    ["id_token_hint"] = idToken,
                    ["post_logout_redirect_uri"] = redirectUri
                });
    }
}
