namespace BankSystem.Web.Infrastructure.Kyc
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authentication.Cookies;

    public interface IKycTokenRefresher
    {
        Task RefreshIfExpiredAsync(CookieValidatePrincipalContext context);
    }
}
