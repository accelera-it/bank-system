# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

BankSystem is an ASP.NET Core (.NET 10) solution (`BankSystem.sln` at the repo root) made of three independently deployed web apps:

- **BankSystem.Web** (`core/src/BankSystem.Web`) — the bank: MVC + Razor Pages (Identity under `Pages/Account`), feature areas `Cards`, `MoneyTransfers`, `Administration`.
- **CentralApi** (`core/src/CentralApi`) — a hub that connects multiple BankSystem instances and routes global transfers, card payments and direct payments between them.
- **DemoShop** (`demo/`) — sample merchant site that pays via CentralApi (direct and card payments).

Repo layout (mirrored by the `core` and `demo` solution folders in `BankSystem.sln`): `core/` holds the bank and CentralApi, `demo/` holds DemoShop. Each has `src/`, `tests/`, `data/` (one Dockerfile per backing service), its own compose files and env file; there is no compose file at the root. `./rsa-keygen.sh [bits]` (openssl) generates RSA key pairs in the .NET `<RSAKeyValue>` XML format used in config. Setup and feature docs live in the GitHub wiki (https://github.com/banksystembg/BankSystem/wiki), linked from README.md.

## Commands

All projects target `net10.0` through `Directory.Build.props`; package versions live in `Directory.Packages.props` (central package management). `dotnet tool restore` installs `dotnet-ef`.

```bash
dotnet restore
dotnet build --no-restore
dotnet test                                                        # all six test projects, no database needed
dotnet test core/tests/BankSystem.UnitTests                        # one project
dotnet test --filter "FullyQualifiedName~MoneyTransferServiceTests" # one class
dotnet test --filter "FullyQualifiedName~MoneyTransferServiceTests.MethodName" # one test
dotnet run --project core/src/BankSystem.Web
dotnet run --project core/src/CentralApi
```

EF Core migrations (Oracle for BankSystem and CentralApi, PostgreSQL for DemoShop; each app calls `Database.Migrate()` on startup):

```bash
dotnet ef migrations add <Name> --project core/src/BankSystem.Data --startup-project core/src/BankSystem.Web
dotnet ef migrations add <Name> --project core/src/CentralApi.Data --startup-project core/src/CentralApi
dotnet ef migrations add <Name> --project demo/src/DemoShop.Data --startup-project demo/src/DemoShop.Web
```

Docker (build context is the repo root; each app has its own `Dockerfile`). Start core first: it creates the `bank_lan` network DemoShop uses to reach CentralApi.

```bash
docker compose -f core/docker-compose.yml up -d --build   # CentralApi :8080, BankSystem.Web :8081 (oracle, bank-redis internal only)
docker compose -f demo/docker-compose.yml up -d --build   # DemoShop :8082 (pg, shop-redis internal only)
docker compose -f core/docker-compose.test.yml up --build --abort-on-container-failure   # four core test projects on EF Core InMemory
docker compose -f demo/docker-compose.test.yml up --build --abort-on-container-failure   # two demo test projects
docker compose -f core/docker-compose.test.yml run --rm --build api-unit   # one project: {web,api}-{unit,integration}, demo-{unit,integration} in demo/
```

Every service has a `healthcheck` in its compose file (not in the Dockerfiles): the apps answer `GET /health` (liveness only), and `depends_on` waits for `service_healthy`.

Each stack loads its own env file through `env_file` (`core/bank.env`, `demo/shop.env`), which overrides connection strings and inter-service URLs (`Section__Key`: `ConnectionStrings__BankSystem`, `__CentralApi`, `__DemoShop`, `__Redis`). Apps listen on port 5000 inside the containers.

## Architecture

### Layering
Each app follows the same layered layout, split into separate projects per layer:
`*.Models` (EF entities) → `*.Data` (DbContext + migrations) → `*.Services.Models` (DTOs) → `*.Services` → web project. Every project sits directly under `core/src/` or `demo/src/`. `core/src/BankSystem.Common` is shared by BankSystem and CentralApi (config classes, crypto utils, email sender, Mapster setup).

### Redis
`RedisServiceCollectionExtensions.AddRedisSessions` (one copy in BankSystem.Web, one in DemoShop.Web) puts `IDistributedCache`, login tickets (`RedisTicketStore`) and Data Protection keys in Redis, so the auth cookie only carries a key. Without `ConnectionStrings:Redis` (tests, plain `dotnet run`) the cache lives in memory. CentralApi has no logins or cache, so it does not use Redis.

### Convention-based DI
`core/src/BankSystem.Web/Infrastructure/Extensions/ServiceCollectionExtensions.cs` registers every class `Foo` that implements an interface named `IFoo` — scoped for the Services and Common assemblies, transient for the web assembly. New services need no manual registration as long as they follow the `Foo`/`IFoo` naming.

### Convention-based Mapster mapping
`core/src/BankSystem.Common/Mapping/MappingConfig.cs` scans the assemblies passed to `AddMapping`/`MappingConfig.Create`:
- types implementing `IMapWith<T>` get a bidirectional map with `T`;
- types implementing `IHaveCustomMapping` configure their own maps.
Add mappings by implementing these interfaces on the DTO/view model.

### Inter-bank security protocol
All bank ↔ CentralApi traffic uses hybrid encryption + RSA signatures (`core/src/BankSystem.Common/Utils/SignatureVerificationUtil.cs`, `CryptographyExtensions.cs`, `RsaExtensions.cs`):
- Sender signs with its private key and encrypts an AES key/IV with the recipient's public key; the payload is a Base64 JSON `{ EncryptedKey, EncryptedIv, Data, Signature }`.
- Receivers use a `[DecryptAndVerifyRequest]` action filter (one in BankSystem.Web, one in CentralApi) that decrypts, verifies, and **replaces the action's first argument** with the decrypted JSON string, returning 403 on failure. API actions therefore take `[FromBody] string data` and deserialize it themselves.
- Bank API endpoints: `core/src/BankSystem.Web/Api/` (`ReceiveMoneyTransfers`, `CardPayments`). CentralApi endpoints: `ReceiveTransactions`, `CardPayments`, `Payments`.

### Configuration and multi-bank setup
- Bank identity comes from the `BankConfiguration` section (name, `UniqueIdentifier` = swift code, country, `First3CardDigits`, own RSA `Key`, `CentralApiPublicKey`, `CentralApiAddress`). It and `SendGridConfiguration` are validated in `PostConfigure`, so startup throws if a value is missing.
- CentralApi knows banks through its `Banks` table (`core/src/CentralApi.Models/Bank.cs`: public `ApiKey`, `ApiAddress`, `PaymentUrl`, `CardPaymentUrl`, `BankIdentificationCardNumbers`). It routes transfers by matching name + swift code + country, and card payments by the card's first 3 digits. A default bank is seeded in `core/src/CentralApi/Infrastructure/ApplicationBuilderExtensions.cs`, at the address in the `DefaultBankBaseUrl` setting, with its browser-facing payment page at `DefaultBankPublicUrl`.
- BankSystem.Web seeds the `admin@abs.uz` / `123456` user with an account and the Administrator role on startup (its password bypasses the policy by being hashed directly), and DemoShop seeds `demo@abs.uz` / `123456` and a laptop product (`Infrastructure/Extensions/ApplicationBuilderExtensions.cs`).

### Tests
Six xUnit projects (FluentAssertions, Moq): `core/tests/{BankSystem,CentralApi}.{UnitTests,IntegrationTests}` and `demo/tests/DemoShop.{UnitTests,IntegrationTests}`.
- Unit tests cover services and crypto/payment helpers on an EF Core InMemory DB.
- Integration tests boot the real app with `WebApplicationFactory`, one instance and database per test class. Helpers are linked from `core/tests/Shared` and `demo/tests/Shared`: `TestDatabase` (EF Core InMemory, so the seeders call `EnsureCreated` instead of migrating; real Oracle and PostgreSQL are not covered by tests), `TestAuthHandler` (sign in through headers), `StubHttpServer` (loopback server for the next hop, because the apps create their own `HttpClient`) and `SecureRequestBuilder` (the inter-bank protocol).
