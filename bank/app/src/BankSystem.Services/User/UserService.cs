namespace BankSystem.Services.User
{
    using System.Threading.Tasks;
    using Data;
    using Microsoft.EntityFrameworkCore;

    public class UserService : BaseService, IUserService
    {
        public UserService(BankSystemDbContext context)
            : base(context)
        {
        }

        public async Task<string> GetAccountOwnerFullnameAsync(string userId)
        {
            var user = await this.Context
                .Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.Id == userId);

            return user?.FullName;
        }
    }
}