namespace BankSystem.Web.Infrastructure.Extensions
{
    using System.Threading.Tasks;
    using Common.Configuration;
    using Kyc;
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
        public const string ActionStatusKey = "kc_action_status";
        public const string ActionSucceeded = "success";
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
            services.AddHttpClient<IKycTokenRefresher, KycTokenRefresher>();

            services.ConfigureApplicationCookie(options =>
                options.Events.OnValidatePrincipal = async context =>
                {
                    await SecurityStampValidator.ValidatePrincipalAsync(context);
                    if (context.Principal != null)
                    {
                        await context.HttpContext.RequestServices
                            .GetRequiredService<IKycTokenRefresher>()
                            .RefreshIfExpiredAsync(context);
                    }
                });

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
                    options.Events.OnAuthorizationCodeReceived = context =>
                    {
                        var status = context.ProtocolMessage.GetParameter(ActionStatusKey);
                        if (status != null)
                        {
                            context.Properties.Items[ActionStatusKey] = status;
                        }

                        return Task.CompletedTask;
                    };
                    options.Events.OnRedirectToIdentityProvider = context =>
                    {
                        if (context.Properties.Items.TryGetValue(PromptKey, out var prompt))
                        {
                            context.ProtocolMessage.Prompt = prompt;
                        }

                        if (context.Properties.Items.TryGetValue(ActionKey, out var action))
                        {
                            context.ProtocolMessage.SetParameter(ActionKey, action);

                            // Keycloak's password form has no current password: this asks for it first
                            if (action == UpdatePasswordAction)
                            {
                                context.ProtocolMessage.MaxAge = "0";
                            }
                        }

                        return Task.CompletedTask;
                    };
                });

            return services;
        }
    }
}
