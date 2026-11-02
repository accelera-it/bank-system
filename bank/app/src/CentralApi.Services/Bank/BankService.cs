namespace CentralApi.Services.Bank
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Mapster;
    using MapsterMapper;
    using Data;
    using Microsoft.EntityFrameworkCore;
    using Models.Banks;

    public class BanksService : BaseService, IBanksService
    {
        private readonly IMapper mapper;
        
        public BanksService(CentralApiDbContext context, IMapper mapper)
            : base(context)
            => this.mapper = mapper;

        public async Task<T> GetBankAsync<T>(string bankName, string swiftCode, string bankCountry)
            where T : BankBaseServiceModel
        {
            const string likeExpression = "%{0}%";
            var bank = await this.Context
                .Banks
                .Where(b =>
                    EF.Functions.Like(b.Name.ToUpper(), string.Format(likeExpression, bankName).ToUpper()) &&
                    EF.Functions.Like(b.SwiftCode.ToUpper(), string.Format(likeExpression, swiftCode).ToUpper()) &&
                    EF.Functions.Like(b.Location.ToUpper(), string.Format(likeExpression, bankCountry).ToUpper()))
                .ProjectToType<T>(this.mapper.Config)
                .SingleOrDefaultAsync();

            return bank;
        }

        public async Task<IEnumerable<T>> GetAllBanksSupportingPaymentsAsync<T>()
            where T : BankBaseServiceModel
        {
            var banks = await this.Context
                .Banks
                .AsNoTracking()
                .Where(b => b.PaymentUrl != null)
                .OrderBy(b => b.Location)
                .ThenBy(b => b.Name)
                .ProjectToType<T>(this.mapper.Config)
                .ToArrayAsync();

            return banks;
        }

        public async Task<T> GetBankByIdAsync<T>(string id)
            where T : BankBaseServiceModel
        {
            return await this.Context
                .Banks
                .AsNoTracking()
                .Where(b => b.Id == id)
                .ProjectToType<T>(this.mapper.Config)
                .SingleOrDefaultAsync();
        }

        public async Task<T> GetBankByBankIdentificationCardNumbersAsync<T>(string identificationCardNumbers)
            where T : BankBaseServiceModel
            => await this.Context
                .Banks
                .AsNoTracking()
                .Where(b => b.BankIdentificationCardNumbers == identificationCardNumbers)
                .ProjectToType<T>(this.mapper.Config)
                .SingleOrDefaultAsync();
    }
}