namespace DemoShop.UnitTests
{
    using System.Linq;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Services.Implementations;
    using Services.Models.Product;
    using Xunit;

    public class ProductsServiceTests : ServiceTestBase
    {
        private readonly ProductsService service;

        public ProductsServiceTests() => this.service = new ProductsService(this.Context);

        private static ProductCreateServiceModel Valid(string name = "Book", decimal price = 10)
            => new ProductCreateServiceModel { Name = name, Price = price, ImageUrl = "https://shop.test/a.png" };

        [Fact]
        public async Task CreateAsync_WithValidModel_ShouldStoreProduct()
        {
            await this.service.CreateAsync(Valid());

            this.Context.Products.Single().Name.Should().Be("Book");
        }

        [Theory]
        [InlineData(null, 10)]
        [InlineData("Book", 0)]
        public async Task CreateAsync_WithInvalidModel_ShouldNotStoreProduct(string name, decimal price)
        {
            await this.service.CreateAsync(Valid(name, price));

            this.Context.Products.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateAsync_WithInvalidImageUrl_ShouldNotStoreProduct()
        {
            var model = Valid();
            model.ImageUrl = "not a url";

            await this.service.CreateAsync(model);

            this.Context.Products.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnProductsOrderedByName()
        {
            await this.service.CreateAsync(Valid("Zebra"));
            await this.service.CreateAsync(Valid("Apple"));

            var result = await this.service.GetAllAsync();

            result.Select(p => p.Name).Should().ContainInOrder("Apple", "Zebra");
        }
    }
}
