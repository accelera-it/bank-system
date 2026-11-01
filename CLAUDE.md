# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

BankSystem is three independently deployed web apps; the first two are an ASP.NET Core (.NET 10) solution (`core/BankSystem.sln`):

- **BankSystem.Web** (`core/src/BankSystem.Web`) — the bank: MVC + Razor Pages (Identity under `Pages/Account`), feature areas `Cards`, `MoneyTransfers`, `Administration`.
- **CentralApi** (`core/src/CentralApi`) — a hub that connects multiple BankSystem instances and routes global transfers, card payments and direct payments between them.
- **DemoShop** (`demo/`) — sample merchant that pays via CentralApi (direct and card payments): Python FastAPI middleware (`demo/api`), and one React + TypeScript + Vite app (`demo/app`): the Tailwind CDN shop site and, under `/cabinet`, the Ant Design cabinet. It is not part of `BankSystem.sln`.

In `core`, `core/proxy` (nginx) is the only public entry (:8081): `/api/` goes to `core/src/CentralApi.Gateway` (Ocelot, routes in `ocelot.json`), everything else to BankSystem.Web. Ocelot takes every call to CentralApi, including those from the bank and the shop; CentralApi calls the bank's API on `web` directly. `/pay` exists in both apps, so CentralApi's page is public as `/api/pay` and Ocelot sends `X-Forwarded-Prefix` so its links keep the prefix.

Repo layout: `core/` holds the bank and CentralApi (`src/`, `tests/`, `proxy/` with nginx for the web app, `data/`), `demo/` holds DemoShop (`api/` with its package in `src/`, `app/`, `data/`). `data/` has one Dockerfile per backing service, and each side has its own compose files and env file; there is no compose file at the root. Demo tests sit next to their code: `demo/api/tests` (pytest) and `demo/app/tests` (Playwright in TypeScript). `README.md` is the overview; `core/README.md` and `demo/README.md` cover setup, tests and configuration for each side. `./rsa-keygen.sh [bits]` (openssl) generates RSA key pairs in the .NET `<RSAKeyValue>` XML format used in config. Setup and feature docs live in the GitHub wiki (https://github.com/banksystembg/BankSystem/wiki), linked from README.md.

## Commands

Run the .NET commands from `core/`, which holds `BankSystem.sln`, `Directory.Build.props` (all projects target `net10.0`), `Directory.Packages.props` (central package management) and `dotnet-tools.json` (`dotnet tool restore` installs `dotnet-ef`).

```bash
dotnet restore
dotnet build --no-restore
dotnet test                                                        # all four test projects, no database needed
dotnet test tests/BankSystem.UnitTests                        # one project
dotnet test --filter "FullyQualifiedName~MoneyTransferServiceTests" # one class
dotnet test --filter "FullyQualifiedName~MoneyTransferServiceTests.MethodName" # one test
dotnet run --project src/BankSystem.Web
dotnet run --project src/CentralApi
```

EF Core migrations (Oracle for BankSystem and CentralApi; each app calls `Database.Migrate()` on startup):

```bash
dotnet ef migrations add <Name> --project src/BankSystem.Data --startup-project src/BankSystem.Web
dotnet ef migrations add <Name> --project src/CentralApi.Data --startup-project src/CentralApi
```

Docker (each app has its own `Dockerfile`; the core build context is `core/`). Start core first: it creates the `bank_lan` network DemoShop uses to reach CentralApi.

```bash
docker compose -f core/docker-compose.yml up -d --build   # nginx :8081: /api -> ocelot -> api, the rest -> web (everything else internal only)
docker compose -f demo/docker-compose.yml up -d --build   # ui :8082 with the cabinet at /cabinet (shop-api, pg, shop-redis, minio internal only)
docker compose -f core/docker-compose.test.yml up --build --abort-on-container-failure   # four core test projects on EF Core InMemory
docker compose -f demo/docker-compose.test.yml up --build --abort-on-container-failure --exit-code-from demo-ui demo-ui   # Playwright, full stack with a CentralApi stub
docker compose -f core/docker-compose.test.yml run --rm --build api-unit   # one project: {web,api}-{unit,integration}; demo-api (pytest) in demo/
```

Every service has a `healthcheck` in its compose file (not in the Dockerfiles): the apps answer `GET /health` (liveness only), and `depends_on` waits for `service_healthy`.

Each stack loads its own env file through `env_file` (`core/bank.env`, `demo/shop.env`), which holds connection strings, inter-service URLs, RSA keys and every other setting; `appsettings.json` only has logging defaults, and `ASPNETCORE_ENVIRONMENT=Development` is set in the Dockerfiles (`Section__Key`: `ConnectionStrings__BankSystem`, `__CentralApi`, `__Redis`; `shop.env` uses plain names such as `DATABASE_URL`). Apps listen on port 5000 inside the containers.

## Architecture

### Layering
BankSystem and CentralApi follow the same layered layout, split into separate projects per layer:
`*.Models` (EF entities) → `*.Data` (DbContext + migrations) → `*.Services.Models` (DTOs) → `*.Services` → web project. Every project sits directly under `core/src/`. `core/src/BankSystem.Common` is shared by BankSystem and CentralApi (config classes, crypto utils, Mapster setup).

### MinIO (demo)
Product images uploaded in the cabinet go to the `products` bucket through `demo/api/src/storage.py`. The bucket is private: `shop-api` serves images at `/api/products/images/{key}`. The image is `cgr.dev/chainguard/minio`, because the official one is not on the registries.

### Redis
`RedisServiceCollectionExtensions.AddRedisSessions` in BankSystem.Web puts `IDistributedCache`, login tickets (`RedisTicketStore`) and Data Protection keys in Redis, so the auth cookie only carries a key. Without `ConnectionStrings:Redis` (tests, plain `dotnet run`) the cache lives in memory. CentralApi has no logins or cache, so it does not use Redis.

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
- CentralApi serves Swagger at `/swagger` (`/api/swagger/index.html` through nginx) in Development only; it lists endpoints, and unsigned calls still get 403.
- Bank API endpoints: `core/src/BankSystem.Web/Api/` (`ReceiveMoneyTransfers`, `CardPayments`). CentralApi endpoints: `ReceiveTransactions`, `CardPayments`, `Payments`.

### Configuration and multi-bank setup
- Bank identity comes from the `BankConfiguration` section (name, `UniqueIdentifier` = swift code, country, `First3CardDigits`, own RSA `Key`, `CentralApiPublicKey`, `CentralApiAddress`). It is validated in `PostConfigure`, so startup throws if a value is missing.
- CentralApi knows banks through its `Banks` table (`core/src/CentralApi.Models/Bank.cs`: public `ApiKey`, `ApiAddress`, `PaymentUrl`, `CardPaymentUrl`, `BankIdentificationCardNumbers`). It routes transfers by matching name + swift code + country, and card payments by the card's first 3 digits. A default bank is seeded in `core/src/CentralApi/Infrastructure/ApplicationBuilderExtensions.cs`, at the address in the `DefaultBankBaseUrl` setting, with its browser-facing payment page at `DefaultBankPublicUrl`.
- BankSystem.Web seeds the `admin@abs.uz` / `123456` user with an account and the Administrator role on startup (its password bypasses the policy by being hashed directly). Nothing sends email: registration signs the user in straight away, and DemoShop seeds `demo@abs.uz` / `123456` and a laptop product (`demo/api/src/seed.py`).

### Tests
Four xUnit projects (FluentAssertions, Moq) in `core/tests/{BankSystem,CentralApi}.{UnitTests,IntegrationTests}`; the demo has pytest in `demo/api/tests` and Playwright in `demo/app/tests`.
- Unit tests cover services and crypto/payment helpers on an EF Core InMemory DB.
- Integration tests boot the real app with `WebApplicationFactory`, one instance and database per test class. Helpers are linked from `core/tests/Shared`: `TestDatabase` (EF Core InMemory, so the seeders call `EnsureCreated` instead of migrating; real Oracle is not covered by tests), `TestAuthHandler` (sign in through headers), `StubHttpServer` (loopback server for the next hop, because the apps create their own `HttpClient`) and `SecureRequestBuilder` (the inter-bank protocol).
