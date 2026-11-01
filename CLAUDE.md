# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

BankSystem is three independently deployed web apps; the first two are an ASP.NET Core (.NET 10) solution (`bank/BankSystem.sln`):

- **BankSystem.Web** (`bank/app/BankSystem.Web`) — the bank: MVC + Razor Pages (Identity under `Pages/Account`), feature areas `Cards`, `MoneyTransfers`, `Administration`.
- **CentralApi** (`bank/app/CentralApi`) — a hub that connects multiple BankSystem instances and routes global transfers, card payments and direct payments between them.
- **DemoShop** (`shop/`) — sample merchant that pays via CentralApi (direct and card payments): Python FastAPI middleware (`shop/api`), and one React + TypeScript + Vite app (`shop/app`): the Tailwind CDN shop site and, under `/cabinet`, the Ant Design cabinet. It is not part of `BankSystem.sln`.

In `bank`, `bank/proxy` (nginx) is the only public entry (:8081): `/api/` goes to `bank/app/CentralApi.Gateway` (Ocelot, routes in `ocelot.json`), everything else to BankSystem.Web. Ocelot takes every call to CentralApi, including those from the bank and the shop; CentralApi calls the bank's API on `web` directly. `/pay` exists in both apps, so CentralApi's page is public as `/api/pay` and Ocelot sends `X-Forwarded-Prefix` so its links keep the prefix.

Repo layout: `kyc/` holds Keycloak (`app/` with Java tests in `app/tests`, `data/`, `proxy/`); it runs on its own at :8080 and neither app signs in through it yet. `bank/` holds the bank and CentralApi (`app/`, `tests/`, `proxy/` with nginx for the web app, `data/`), `shop/` holds DemoShop (`api/` with its package in `src/`, `app/`, `data/`). `data/` has one Dockerfile per backing service, and each side has its own compose files and env file; there is no compose file at the root. Demo tests sit next to their code: `shop/api/tests` (pytest) and `shop/app/tests` (Playwright in TypeScript). `README.md` is the overview; `bank/README.md` and `shop/README.md` cover setup, tests and configuration for each side. `./rsa-keygen.sh [bits]` (openssl) generates RSA key pairs in the .NET `<RSAKeyValue>` XML format used in config. Setup and feature docs live in the GitHub wiki (https://github.com/banksystembg/BankSystem/wiki), linked from README.md.

## Commands

Run the .NET commands from `bank/`, which holds `BankSystem.sln`, `Directory.Build.props` (all projects target `net10.0`), `Directory.Packages.props` (central package management) and `dotnet-tools.json` (`dotnet tool restore` installs `dotnet-ef`).

```bash
dotnet restore
dotnet build --no-restore
dotnet test                                                        # all four test projects, no database needed
dotnet test app/BankSystem.UnitTests                        # one project
dotnet test --filter "FullyQualifiedName~MoneyTransferServiceTests" # one class
dotnet test --filter "FullyQualifiedName~MoneyTransferServiceTests.MethodName" # one test
dotnet run --project app/BankSystem.Web
dotnet run --project app/CentralApi
```

EF Core migrations (Oracle for BankSystem and CentralApi; each app calls `Database.Migrate()` on startup):

```bash
dotnet ef migrations add <Name> --project app/BankSystem.Data --startup-project app/BankSystem.Web
dotnet ef migrations add <Name> --project app/CentralApi.Data --startup-project app/CentralApi
```

Docker (each app has its own `Dockerfile`; the bank build context is `bank/`). Start bank first: it creates the `bank_lan` network DemoShop uses to reach CentralApi.

```bash
docker compose -f bank/docker-compose.yml up -d --build   # nginx :8081: /api -> ocelot -> api, the rest -> web (everything else internal only)
docker compose -f shop/docker-compose.yml up -d --build   # ui :8082 with the cabinet at /cabinet (shop-api, pg, shop-redis, minio internal only)
docker compose -f bank/docker-compose.test.yml up --build --abort-on-container-failure   # four bank test projects on EF Core InMemory
docker compose -f shop/docker-compose.test.yml up --build --abort-on-container-failure --exit-code-from ui-tests ui-tests   # Playwright, full stack with a CentralApi stub
docker compose -f bank/docker-compose.test.yml run --rm --build api-unit   # one project: {web,api}-{unit,integration}; api-tests (pytest) in shop/
```

Every service has a `healthcheck` in its compose file (not in the Dockerfiles): the apps answer `GET /health` (liveness only), and `depends_on` waits for `service_healthy`.

Each stack loads its own env file through `env_file` (`bank/bank.env`, `shop/shop.env`), which holds connection strings, inter-service URLs, RSA keys and every other setting; `appsettings.json` only has logging defaults, and `ASPNETCORE_ENVIRONMENT=Development` is set in the Dockerfiles (`Section__Key`: `ConnectionStrings__BankSystem`, `__CentralApi`, `__Redis`; `shop.env` uses plain names such as `DATABASE_URL`). Apps listen on port 5000 inside the containers.

## Architecture

### Layering
BankSystem and CentralApi follow the same layered layout, split into separate projects per layer:
`*.Models` (EF entities) → `*.Data` (DbContext + migrations) → `*.Services.Models` (DTOs) → `*.Services` → web project. Every project sits directly under `bank/app/`. `bank/app/BankSystem.Common` is shared by BankSystem and CentralApi (config classes, crypto utils, Mapster setup).

### MinIO (shop)
Product images uploaded in the cabinet go to the `products` bucket through `shop/api/src/storage.py`. The bucket is private: `shop-api` serves images at `/api/products/images/{key}`. The image is `cgr.dev/chainguard/minio`, because the official one is not on the registries.

### Redis
`RedisServiceCollectionExtensions.AddRedisSessions` in BankSystem.Web puts `IDistributedCache`, login tickets (`RedisTicketStore`) and Data Protection keys in Redis, so the auth cookie only carries a key. Without `ConnectionStrings:Redis` (tests, plain `dotnet run`) the cache lives in memory. CentralApi has no logins or cache, so it does not use Redis.

### Convention-based DI
`bank/app/BankSystem.Web/Infrastructure/Extensions/ServiceCollectionExtensions.cs` registers every class `Foo` that implements an interface named `IFoo` — scoped for the Services and Common assemblies, transient for the web assembly. New services need no manual registration as long as they follow the `Foo`/`IFoo` naming.

### Convention-based Mapster mapping
`bank/app/BankSystem.Common/Mapping/MappingConfig.cs` scans the assemblies passed to `AddMapping`/`MappingConfig.Create`:
- types implementing `IMapWith<T>` get a bidirectional map with `T`;
- types implementing `IHaveCustomMapping` configure their own maps.
Add mappings by implementing these interfaces on the DTO/view model.

### Inter-bank security protocol
All bank ↔ CentralApi traffic uses hybrid encryption + RSA signatures (`bank/app/BankSystem.Common/Utils/SignatureVerificationUtil.cs`, `CryptographyExtensions.cs`, `RsaExtensions.cs`):
- Sender signs with its private key and encrypts an AES key/IV with the recipient's public key; the payload is a Base64 JSON `{ EncryptedKey, EncryptedIv, Data, Signature }`.
- Receivers use a `[DecryptAndVerifyRequest]` action filter (one in BankSystem.Web, one in CentralApi) that decrypts, verifies, and **replaces the action's first argument** with the decrypted JSON string, returning 403 on failure. API actions therefore take `[FromBody] string data` and deserialize it themselves.
- CentralApi serves Swagger at `/swagger` (`/api/swagger/index.html` through nginx) in Development only; it lists endpoints, and unsigned calls still get 403.
- Bank API endpoints: `bank/app/BankSystem.Web/Api/` (`ReceiveMoneyTransfers`, `CardPayments`). CentralApi endpoints: `ReceiveTransactions`, `CardPayments`, `Payments`.

### Configuration and multi-bank setup
- Bank identity comes from the `BankConfiguration` section (name, `UniqueIdentifier` = swift code, country, `First3CardDigits`, own RSA `Key`, `CentralApiPublicKey`, `CentralApiAddress`). It is validated in `PostConfigure`, so startup throws if a value is missing.
- CentralApi knows banks through its `Banks` table (`bank/app/CentralApi.Models/Bank.cs`: public `ApiKey`, `ApiAddress`, `PaymentUrl`, `CardPaymentUrl`, `BankIdentificationCardNumbers`). It routes transfers by matching name + swift code + country, and card payments by the card's first 3 digits. A default bank is seeded in `bank/app/CentralApi/Infrastructure/ApplicationBuilderExtensions.cs`, at the address in the `DefaultBankBaseUrl` setting, with its browser-facing payment page at `DefaultBankPublicUrl`.
- BankSystem.Web seeds the `admin@abs.uz` / `123456` user with an account and the Administrator role on startup (its password bypasses the policy by being hashed directly). Nothing sends email: registration signs the user in straight away, and DemoShop seeds `demo@abs.uz` / `123456` and a laptop product (`shop/api/src/seed.py`).

### Tests
Four xUnit projects (FluentAssertions, Moq) in `bank/app/{BankSystem,CentralApi}.{UnitTests,IntegrationTests}`; the demo has pytest in `shop/api/tests` and Playwright in `shop/app/tests`.
- Unit tests cover services and crypto/payment helpers on an EF Core InMemory DB.
- Integration tests boot the real app with `WebApplicationFactory`, one instance and database per test class. Helpers are linked from `bank/app/Shared`: `TestDatabase` (EF Core InMemory, so the seeders call `EnsureCreated` instead of migrating; real Oracle is not covered by tests), `TestAuthHandler` (sign in through headers), `StubHttpServer` (loopback server for the next hop, because the apps create their own `HttpClient`) and `SecureRequestBuilder` (the inter-bank protocol).
