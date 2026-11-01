namespace BankSystem.Web.Infrastructure.Helpers.GlobalTransferHelpers.Models
{
    public enum GlobalTransferResult
    {
        Succeeded,
        InsufficientFunds,
        GeneralFailure,

        // The money is held: it will reach the recipient or come back without further action
        Pending
    }
}