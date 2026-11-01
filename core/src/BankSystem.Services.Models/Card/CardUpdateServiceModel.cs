namespace BankSystem.Services.Models.Card
{
    using System.ComponentModel.DataAnnotations;
    using BankSystem.Common;

    public class CardUpdateServiceModel
    {
        [Required]
        public string Id { get; set; }

        [Required]
        [RegularExpression(ModelConstants.Card.NumberRegex)]
        public string Number { get; set; }

        [Required]
        [MaxLength(ModelConstants.Card.NameMaxLength)]
        public string Name { get; set; }

        [Required]
        [RegularExpression(ModelConstants.Card.ExpiryDateRegex)]
        public string ExpiryDate { get; set; }

        [Required]
        [RegularExpression(ModelConstants.Card.SecurityCodeRegex)]
        public string SecurityCode { get; set; }
    }
}
