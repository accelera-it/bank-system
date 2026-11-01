namespace BankSystem.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using Common;

    /// <summary>
    /// A global transfer whose money has left the account and whose delivery is not confirmed yet.
    /// </summary>
    public class PendingTransfer
    {
        [Key]
        [MaxLength(ModelConstants.MoneyTransfer.ReferenceNumberMaxLength)]
        public string ReferenceNumber { get; set; }

        [Required]
        public string AccountId { get; set; }

        public BankAccount Account { get; set; }

        [Required]
        public string Payload { get; set; }

        [Required]
        public DateTime CreatedOn { get; set; }

        public int Attempts { get; set; }

        public DateTime? LastAttemptOn { get; set; }
    }
}
