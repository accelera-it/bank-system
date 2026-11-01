# Bank

Guidance for the bank and CentralApi; the root `CLAUDE.md` has the overview and commands.

## Redis
`RedisServiceCollectionExtensions.AddRedisSessions` in BankSystem.Web puts `IDistributedCache`, login tickets (`RedisTicketStore`) and Data Protection keys in Redis, so the auth cookie only carries a key. Without `ConnectionStrings:Redis` (tests, plain `dotnet run`) the cache lives in memory. CentralApi has no logins or cache, so it does not use Redis.

## Convention-based DI
`bank/app/src/BankSystem.Web/Infrastructure/Extensions/ServiceCollectionExtensions.cs` registers every class `Foo` that implements an interface named `IFoo` — scoped for the Services and Common assemblies, transient for the web assembly. New services need no manual registration as long as they follow the `Foo`/`IFoo` naming.

## Convention-based Mapster mapping
`bank/app/core/BankSystem.Common/Mapping/MappingConfig.cs` scans the assemblies passed to `AddMapping`/`MappingConfig.Create`:
- types implementing `IMapWith<T>` get a bidirectional map with `T`;
- types implementing `IHaveCustomMapping` configure their own maps.
Add mappings by implementing these interfaces on the DTO/view model.

## Inter-bank security protocol
All bank ↔ CentralApi traffic uses hybrid encryption + RSA signatures (`bank/app/core/BankSystem.Common/Utils/SignatureVerificationUtil.cs`, `CryptographyExtensions.cs`, `RsaExtensions.cs`):
- Sender signs with its private key and encrypts an AES key/IV with the recipient's public key; the payload is a Base64 JSON `{ EncryptedKey, EncryptedIv, Data, Signature }`.
- Receivers use a `[DecryptAndVerifyRequest]` action filter (one in BankSystem.Web, one in CentralApi) that decrypts, verifies, and **replaces the action's first argument** with the decrypted JSON string, returning 403 on failure. API actions therefore take `[FromBody] string data` and deserialize it themselves.
- CentralApi serves Swagger at `/swagger` (`/api/swagger/index.html` through nginx) in Development only; it lists endpoints, and unsigned calls still get 403.
- Bank API endpoints: `bank/app/src/BankSystem.Web/Api/` (`ReceiveMoneyTransfers`, `CardPayments`). CentralApi endpoints: `ReceiveTransactions`, `CardPayments`, `Payments`.

## Configuration and multi-bank setup
- Bank identity comes from the `BankConfiguration` section (name, `UniqueIdentifier` = swift code, country, `First3CardDigits`, own RSA `Key`, `CentralApiPublicKey`, `CentralApiAddress`). It is validated in `PostConfigure`, so startup throws if a value is missing.
- CentralApi knows banks through its `Banks` table (`bank/app/src/CentralApi.Models/Bank.cs`: public `ApiKey`, `ApiAddress`, `PaymentUrl`, `CardPaymentUrl`, `BankIdentificationCardNumbers`). It routes transfers by matching name + swift code + country, and card payments by the card's first 3 digits. A default bank is seeded in `bank/app/src/CentralApi/Infrastructure/ApplicationBuilderExtensions.cs`, at the address in the `DefaultBankBaseUrl` setting, with its browser-facing payment page at `DefaultBankPublicUrl`.
