namespace BankSystem.Web.Infrastructure.Kyc
{
    using System.Threading.Tasks;
    using BankSystem.Models;

    public interface IKycUserService
    {
        Task<BankUser> FindOrCreateAsync(string email, string fullName);
    }
}
