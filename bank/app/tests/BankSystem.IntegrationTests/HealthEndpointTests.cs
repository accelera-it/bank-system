namespace BankSystem.IntegrationTests
{
    using System.Net;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Xunit;

    public class HealthEndpointTests : IClassFixture<BankWebFactory>
    {
        private readonly BankWebFactory factory;

        public HealthEndpointTests(BankWebFactory factory) => this.factory = factory;

        [Fact]
        public async Task Health_ReturnsOk_WithoutAuthentication()
        {
            using var client = this.factory.CreateClient();

            var response = await client.GetAsync("/health");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
