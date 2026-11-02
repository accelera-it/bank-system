namespace CentralApi.Models
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Bank
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }

        [Required]
        [MaxLength(256)]
        public string Name { get; set; }

        [Required]
        [MaxLength(32)]
        public string SwiftCode { get; set; }

        [Required]
        [MaxLength(256)]
        public string Location { get; set; }

        [Required]
        public string ApiKey { get; set; }

        [Required]
        public string ApiAddress { get; set; }

        public string PaymentUrl { get; set; }

        public string CardPaymentUrl { get; set; }

        [MaxLength(16)]
        public string BankIdentificationCardNumbers { get; set; }
    }
}