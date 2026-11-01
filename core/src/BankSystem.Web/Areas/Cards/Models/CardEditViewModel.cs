namespace BankSystem.Web.Areas.Cards.Models
{
    using System.ComponentModel.DataAnnotations;
    using Common;

    public class CardEditViewModel
    {
        [Required]
        public string Id { get; set; }

        [Display(Name = "Linked account")]
        public string AccountName { get; set; }

        [Required]
        [MaxLength(ModelConstants.Card.NameMaxLength)]
        [Display(Name = "Cardholder")]
        public string Name { get; set; }

        [Required]
        [RegularExpression(ModelConstants.Card.NumberRegex)]
        [Display(Name = "Card number")]
        public string Number { get; set; }

        [Required]
        [RegularExpression(ModelConstants.Card.ExpiryDateRegex)]
        [Display(Name = "Expires")]
        public string ExpiryDate { get; set; }

        [Required]
        [RegularExpression(ModelConstants.Card.SecurityCodeRegex)]
        [Display(Name = "CVV")]
        public string SecurityCode { get; set; }
    }
}
