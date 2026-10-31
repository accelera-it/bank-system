namespace BankSystem.Common.Mapping.Interfaces
{
    using Mapster;

    public interface IHaveCustomMapping
    {
        void ConfigureMapping(TypeAdapterConfig config);
    }
}
