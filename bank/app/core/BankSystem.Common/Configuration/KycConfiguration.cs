namespace BankSystem.Common.Configuration
{
    public class KycConfiguration
    {
        // Where this app reaches Keycloak, inside the Docker network
        public string Authority { get; set; }

        // Where browsers reach Keycloak
        public string PublicAuthority { get; set; }

        public string ClientId { get; set; }
    }
}
