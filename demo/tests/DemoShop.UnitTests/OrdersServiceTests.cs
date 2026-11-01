namespace DemoShop.UnitTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using DemoShop.Models;
    using FluentAssertions;
    using Microsoft.EntityFrameworkCore;
    using Services.Implementations;
    using Services.Models.Order;
    using Xunit;

    public class OrdersServiceTests : ServiceTestBase
    {
        private const string UserName = "buyer@test.com";

        private readonly OrdersService service;
        private readonly Product product;

        public OrdersServiceTests()
        {
            this.service = new OrdersService(this.Context);
            this.product = new Product { Name = "Book", Price = 10, ImageUrl = "https://shop.test/a.png" };
            this.Context.Users.Add(new DemoShopUser { UserName = UserName });
            this.Context.Products.Add(this.product);
            this.Context.SaveChanges();
        }

        private OrderCreateServiceModel Model(string userName = UserName, string productId = null)
            => new OrderCreateServiceModel
            {
                ProductId = productId ?? this.product.Id,
                UserName = userName,
                CreatedOn = DateTime.UtcNow
            };

        [Fact]
        public async Task CreateAsync_WithValidModel_ShouldCreatePendingOrder()
        {
            var id = await this.service.CreateAsync(this.Model());

            var order = await this.service.GetByIdAsync(id);
            order.PaymentStatus.Should().Be(PaymentStatus.Pending);
            order.ProductName.Should().Be("Book");
            order.UserName.Should().Be(UserName);
        }

        [Fact]
        public async Task CreateAsync_WithUnknownUser_ShouldReturnNull()
            => (await this.service.CreateAsync(this.Model("nobody@test.com"))).Should().BeNull();

        [Fact]
        public async Task CreateAsync_WithUnknownProduct_ShouldReturnNull()
            => (await this.service.CreateAsync(this.Model(productId: "missing"))).Should().BeNull();

        [Fact]
        public async Task CreateAsync_WithMissingUserName_ShouldReturnNull()
        {
            var model = this.Model();
            model.UserName = null;

            (await this.service.CreateAsync(model)).Should().BeNull();
        }

        [Fact]
        public async Task GetAllForUserAsync_ShouldReturnOnlyThatUsersOrdersNewestFirst()
        {
            var older = this.Model();
            older.CreatedOn = DateTime.UtcNow.AddDays(-1);
            var olderId = await this.service.CreateAsync(older);
            var newerId = await this.service.CreateAsync(this.Model());

            var result = (await this.service.GetAllForUserAsync(UserName)).ToArray();

            result.Select(o => o.Id).Should().ContainInOrder(newerId, olderId);
            (await this.service.GetAllForUserAsync("nobody@test.com")).Should().BeEmpty();
        }

        [Fact]
        public async Task SetPaymentStatus_ShouldUpdateOrder()
        {
            var id = await this.service.CreateAsync(this.Model());

            await this.service.SetPaymentStatus(id, PaymentStatus.Completed);

            (await this.Context.Orders.AsNoTracking().SingleAsync()).PaymentStatus.Should().Be(PaymentStatus.Completed);
        }

        [Fact]
        public async Task SetPaymentStatus_WithUnknownOrder_ShouldDoNothing()
        {
            Func<Task> act = () => this.service.SetPaymentStatus("missing", PaymentStatus.Completed);

            await act.Should().NotThrowAsync();
        }
    }
}
