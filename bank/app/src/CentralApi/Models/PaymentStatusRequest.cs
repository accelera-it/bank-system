namespace CentralApi.Models
{
    using System.ComponentModel.DataAnnotations;
    using BankSystem.Common;

    public class PaymentStatusRequest
    {
        [Required]
        [MaxLength(ModelConstants.MoneyTransfer.ReferenceNumberMaxLength)]
        public string ReferenceNumber { get; set; }
    }
}
