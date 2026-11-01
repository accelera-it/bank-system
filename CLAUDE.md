# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

BankSystem is an ASP.NET Core (.NET 10) solution (`BankSystem.sln` at the repo root) made of three independently deployed web apps:

- **BankSystem.Web** (`web/BankSystem.Web`) — the bank: MVC + Razor Pages (Identity under `Pages/Account`), feature areas `Cards`, `MoneyTransfers`, `Administration`.
- **CentralApi** (`web/CentralApi`) — a hub that connects multiple BankSystem instances and routes global transfers, card payments and direct payments between them.
- **DemoShop** (`demo/`) — sample merchant site that pays via CentralApi (direct and card payments).

Repo layout (mirrored by solution folders in `BankSystem.sln`): `core/` (shared libraries and the Models/Data/Services layers of BankSystem and CentralApi), `web/` (BankSystem.Web, CentralApi), `demo/` (DemoShop, all layers), `tests/`. `./rsa-keygen.sh [bits]` (openssl) generates RSA key pairs in the .NET `<RSAKeyValue>` XML format used in config. Setup and feature docs live in the GitHub wiki (https://github.com/banksystembg/BankSystem/wiki), linked from README.md.

## Commands

All projects target `net10.0` through `Directory.Build.props`; package versions live in `Directory.Packages.props` (central package management). `dotnet tool restore` installs `dotnet-ef`.

```bash
dotnet restore
dotnet build --no-restore
dotnet test                                                        # all six test projects, no database needed
dotnet test tests/BankSystem.UnitTests                             # one project
dotnet test --filter "FullyQualifiedName~MoneyTransferServiceTests" # one class
dotnet test --filter "FullyQualifiedName~MoneyTransferServiceTests.MethodName" # one test
dotnet run --project web/BankSystem.Web
dotnet run --project web/CentralApi
```

EF Core migrations (SQL Server; each app calls `Database.Migrate()` on startup):

```bash
dotnet ef migrations add <Name> --project core/BankSystem.Data --startup-project web/BankSystem.Web
dotnet ef migrations add <Name> --project core/CentralApi.Data --startup-project web/CentralApi
```

Docker (build context is the repo root; each app has its own `Dockerfile`):

```bash
docker compose --env-file locale.env up --build   # CentralApi :8080, BankSystem.Web :8081, DemoShop :8082 (db is internal only)
docker compose -f docker-compose.test.yml up --build --abort-on-container-failure   # all six test projects, one service each, on EF Core InMemory
docker compose -f docker-compose.test.yml run --rm --build api-unit   # one project: {web,api,demo}-{unit,integration}
```

Every service has a `healthcheck` in `docker-compose.yml` (not in the Dockerfiles): the apps answer `GET /health` (liveness only), and `depends_on` waits for `service_healthy`.

Every `docker-compose.yml` service loads `locale.env` through `env_file`, which overrides connection strings and inter-service URLs (`Section__Key`, one connection string per app: `ConnectionStrings__BankSystem`, `__CentralApi`, `__DemoShop`). `API_PORT`, `WEB_PORT` and `DEMO_PORT` from that file also set the published ports, so all `docker-compose.yml` commands need `--env-file locale.env`.

## Architecture

### Layering
Each app follows the same layered layout, split into separate projects per layer:
`*.Models` (EF entities) → `*.Data` (DbContext + migrations) → `*.Services.Models` (DTOs) → `*.Services` → web project. Every project sits directly under its top-level folder (no nesting): BankSystem and CentralApi layers in `core/`, DemoShop layers in `demo/`. `core/BankSystem.Common` is shared by BankSystem and CentralApi (config classes, crypto utils, email sender, Mapster setup).

### Convention-based DI
`web/BankSystem.Web/Infrastructure/Extensions/ServiceCollectionExtensions.cs` registers every class `Foo` that implements an interface named `IFoo` — scoped for the Services and Common assemblies, transient for the web assembly. New services need no manual registration as long as they follow the `Foo`/`IFoo` naming.

### Convention-based Mapster mapping
`core/BankSystem.Common/Mapping/MappingConfig.cs` scans the assemblies passed to `AddMapping`/`MappingConfig.Create`:
- types implementing `IMapWith<T>` get a bidirectional map with `T`;
- types implementing `IHaveCustomMapping` configure their own maps.
Add mappings by implementing these interfaces on the DTO/view model.

### Inter-bank security protocol
All bank ↔ CentralApi traffic uses hybrid encryption + RSA signatures (`core/BankSystem.Common/Utils/SignatureVerificationUtil.cs`, `CryptographyExtensions.cs`, `RsaExtensions.cs`):
- Sender signs with its private key and encrypts an AES key/IV with the recipient's public key; the payload is a Base64 JSON `{ EncryptedKey, EncryptedIv, Data, Signature }`.
- Receivers use a `[DecryptAndVerifyRequest]` action filter (one in BankSystem.Web, one in CentralApi) that decrypts, verifies, and **replaces the action's first argument** with the decrypted JSON string, returning 403 on failure. API actions therefore take `[FromBody] string data` and deserialize it themselves.
- Bank API endpoints: `web/BankSystem.Web/Api/` (`ReceiveMoneyTransfers`, `CardPayments`). CentralApi endpoints: `ReceiveTransactions`, `CardPayments`, `Payments`.

### Configuration and multi-bank setup
- Bank identity comes from the `BankConfiguration` section (name, `UniqueIdentifier` = swift code, country, `First3CardDigits`, own RSA `Key`, `CentralApiPublicKey`, `CentralApiAddress`). It and `SendGridConfiguration` are validated in `PostConfigure`, so startup throws if a value is missing.
- CentralApi knows banks through its `Banks` table (`core/CentralApi.Models/Bank.cs`: public `ApiKey`, `ApiAddress`, `PaymentUrl`, `CardPaymentUrl`, `BankIdentificationCardNumbers`). It routes transfers by matching name + swift code + country, and card payments by the card's first 3 digits. A default bank is seeded in `web/CentralApi/Infrastructure/ApplicationBuilderExtensions.cs`, at the address in the `DefaultBankBaseUrl` setting, with its browser-facing payment page at `DefaultBankPublicUrl`.
- BankSystem.Web seeds the `admin@abs.uz` / `123456` user with an account and the Administrator role on startup (its password bypasses the policy by being hashed directly), and DemoShop seeds `demo@abs.uz` / `123456` and a laptop product (`Infrastructure/Extensions/ApplicationBuilderExtensions.cs`).

### Tests
Six xUnit projects (FluentAssertions, Moq): `tests/{BankSystem,CentralApi,DemoShop}.{UnitTests,IntegrationTests}`.
- Unit tests cover services and crypto/payment helpers on an EF Core InMemory DB.
- Integration tests boot the real app with `WebApplicationFactory`, one instance and database per test class. Helpers are linked from `tests/Shared`: `TestDatabase` (SQL Server when `TEST_SQL_CONNECTION` is set, otherwise InMemory, as in `docker-compose.test.yml`, so the seeders call `EnsureCreated` instead of migrating), `TestAuthHandler` (sign in through headers), `StubHttpServer` (loopback server for the next hop, because the apps create their own `HttpClient`) and `SecureRequestBuilder` (the inter-bank protocol).
