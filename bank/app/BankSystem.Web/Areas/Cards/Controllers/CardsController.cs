namespace BankSystem.Web.Areas.Cards.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using MapsterMapper;
    using Common;
    using Infrastructure.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Models;
    using Services.BankAccount;
    using Services.Card;
    using Services.Models.BankAccount;
    using Services.Models.Card;

    public class CardsController : BaseCardsController
    {
        private const int CardsCountPerPage = 10;
        private readonly IBankAccountService bankAccountService;
        private readonly ICardService cardService;
        private readonly IMapper mapper;

        public CardsController(
            IBankAccountService bankAccountService,
            ICardService cardService,
            IMapper mapper)
        {
            this.bankAccountService = bankAccountService;
            this.cardService = cardService;
            this.mapper = mapper;
        }

        public async Task<IActionResult> Index(int pageIndex = 1)
        {
            pageIndex = Math.Max(1, pageIndex);

            var userId = this.GetCurrentUserId();
            var allCards = (await this.cardService
                    .GetCardsAsync<CardDetailsServiceModel>(userId, pageIndex, CardsCountPerPage))
                .Select(this.mapper.Map<CardListingDto>)
                .ToPaginatedList(await this.cardService.GetCountOfAllCardsOwnedByUserAsync(userId), pageIndex,
                    CardsCountPerPage);

            var cards = new CardListingViewModel
            {
                Cards = allCards
            };

            return this.View(cards);
        }

        public async Task<IActionResult> Create()
        {
            // The form only exists as the content of the new card popup
            if (this.Request.Headers.XRequestedWith != "XMLHttpRequest")
            {
                return this.RedirectToAction(nameof(this.Index));
            }

            var userAccounts = await this.GetAllAccountsAsync(this.GetCurrentUserId());

            var model = new CardCreateViewModel
            {
                BankAccounts = userAccounts
            };

            return this.PartialView(model);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CardCreateViewModel model)
        {
            var userId = this.GetCurrentUserId();
            if (!this.ModelState.IsValid)
            {
                this.ShowErrorMessage(NotificationMessages.CardCreateError);

                return this.RedirectToAction(nameof(this.Index));
            }

            var account = await this.bankAccountService.GetByIdAsync<BankAccountDetailsServiceModel>(model.AccountId);
            if (account == null || account.UserId != userId)
            {
                return this.Forbid();
            }

            var serviceModel = this.mapper.Map<CardCreateServiceModel>(model);
            serviceModel.UserId = userId;
            serviceModel.Name = account.UserFullName;
            serviceModel.ExpiryDate = DateTime.UtcNow.AddYears(GlobalConstants.CardValidityInYears)
                .ToString(GlobalConstants.CardExpirationDateFormat, CultureInfo.InvariantCulture);

            bool isCreated = await this.cardService.CreateAsync(serviceModel);
            if (!isCreated)
            {
                this.ShowErrorMessage(NotificationMessages.CardCreateError);

                return this.RedirectToHome();
            }

            this.ShowSuccessMessage(NotificationMessages.CardCreatedSuccessfully);

            return this.RedirectToAction(nameof(this.Index));
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (this.Request.Headers.XRequestedWith != "XMLHttpRequest")
            {
                return this.RedirectToAction(nameof(this.Index));
            }

            var userId = this.GetCurrentUserId();
            var card = await this.cardService.GetAsync<CardDetailsServiceModel>(id);
            if (card == null || card.UserId != userId)
            {
                return this.NotFound();
            }

            return this.PartialView(new CardEditViewModel
            {
                Id = card.Id,
                AccountName = card.AccountName,
                Name = card.Name,
                Number = card.Number,
                ExpiryDate = card.ExpiryDate,
                SecurityCode = card.SecurityCode
            });
        }

        [HttpPost]
        public async Task<IActionResult> Edit(CardEditViewModel model)
        {
            var card = await this.cardService.GetAsync<CardDetailsServiceModel>(model.Id);
            if (card == null || card.UserId != this.GetCurrentUserId())
            {
                this.ShowErrorMessage(NotificationMessages.CardDoesNotExist);

                return this.RedirectToAction(nameof(this.Index));
            }

            var isUpdated = this.ModelState.IsValid && await this.cardService.UpdateAsync(new CardUpdateServiceModel
            {
                Id = card.Id,
                Name = model.Name,
                Number = model.Number,
                ExpiryDate = model.ExpiryDate,
                SecurityCode = model.SecurityCode
            });

            if (isUpdated)
            {
                this.ShowSuccessMessage(NotificationMessages.CardUpdatedSuccessfully);
            }
            else
            {
                this.ShowErrorMessage(NotificationMessages.CardUpdateError);
            }

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                this.ShowErrorMessage(NotificationMessages.CardDoesNotExist);

                return this.RedirectToAction(nameof(this.Index));
            }

            var card = await this.cardService.GetAsync<CardDetailsServiceModel>(id);
            if (card == null || card.UserId != this.GetCurrentUserId())
            {
                this.ShowErrorMessage(NotificationMessages.CardDoesNotExist);

                return this.RedirectToAction(nameof(this.Index));
            }

            var isDeleted = await this.cardService.DeleteAsync(id);
            if (!isDeleted)
            {
                this.ShowErrorMessage(NotificationMessages.CardDeleteError);
            }
            else
            {
                this.ShowSuccessMessage(NotificationMessages.CardDeletedSuccessfully);
            }

            return this.RedirectToAction(nameof(this.Index));
        }

        private async Task<IEnumerable<SelectListItem>> GetAllAccountsAsync(string userId)
            => (await this.bankAccountService
                    .GetAllAccountsByUserIdAsync<BankAccountIndexServiceModel>(userId))
                .Select(a => new SelectListItem { Text = a.Name, Value = a.Id })
                .ToArray();
    }
}