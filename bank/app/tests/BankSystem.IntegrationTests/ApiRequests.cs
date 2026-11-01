namespace BankSystem.IntegrationTests
{
    using System.Net.Http;
    using System.Text;
    using Newtonsoft.Json;

    public static class ApiRequests
    {
        public static StringContent JsonBody(string encodedRequest)
            => new StringContent(JsonConvert.SerializeObject(encodedRequest), Encoding.UTF8, "application/json");
    }
}
