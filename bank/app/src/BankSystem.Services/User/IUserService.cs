namespace BankSystem.Services.User
{
    using System.Threading.Tasks;

    public interface IUserService
    {
        Task<string> GetAccountOwnerFullnameAsync(string userId);
    }
}