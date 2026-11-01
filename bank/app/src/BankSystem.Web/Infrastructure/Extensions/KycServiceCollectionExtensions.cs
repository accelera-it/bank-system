namespace BankSystem.Web.Infrastructure.Extensions
{
    using System.Threading.Tasks;
    using Common.Configuration;
    using Microsoft.AspNetCore.Authentication.OpenIdConnect;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public static class KycServiceCollectionExtensions
    {
        public const string Scheme = "kyc";
        public const string PromptKey = "prompt";
        public const string RegisterPrompt = "create";
        public const string ActionKey = "kc_action";
        public const string UpdateProfileAction = "UPDATE_PROFILE";
        public const string UpdatePasswordAction = "UPDATE_PASSWORD";

        private const string ClientSecretKey = "BANK_CLIENT_SECRET";

        public static IServiceCollection AddKycAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var section = configuration.GetSection(nameof(KycConfiguration));
            var kyc = section.Get<KycConfiguration>();
            services.Configure<KycConfiguration>(section);

            services
                .AddAuthentication()
                .AddOpenIdConnect(Scheme, options =>
                {
                    options.SignInScheme = IdentityConstants.ExternalScheme;
                    options.Authority = kyc?.Authority;
                    options.RequireHttpsMetadata = false;
                    options.ClientId = kyc?.ClientId;
                    options.ClientSecret = configuration[ClientSecretKey];
                    options.ResponseType = "code";
                    options.Scope.Add("email");
                    options.Scope.Add("profile");
                    options.SaveTokens = true;
                    options.MapInboundClaims = false;
                    options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
                    options.NonceCookie.SecurePolicy = CookieSecurePolicy.None;
                    options.NonceCookie.SameSite = SameSiteMode.Lax;
                    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None;
                    options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                    options.Events.OnRedirectToIdentityProvider = context =>
                    {
                        if (context.Properties.Items.TryGetValue(PromptKey, out var prompt))
                        {
                            context.ProtocolMessage.Prompt = prompt;
                        }

                        if (context.Properties.Items.TryGetValue(ActionKey, out var action))
                        {
                            context.ProtocolMessage.SetParameter(ActionKey, action);
                        }

                        return Task.CompletedTask;
                    };
                });

            return services;
        }
    }
}
