namespace BankSystem.Services.Models.MoneyTransfer
{
    using System;

    public class PendingTransferListingServiceModel
    {
        public string ReferenceNumber { get; set; }

        public string AccountUniqueId { get; set; }

        public decimal Amount { get; set; }

        public DateTime CreatedOn { get; set; }

        public int Attempts { get; set; }
    }
}
