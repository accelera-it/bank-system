# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

BankSystem is an ASP.NET Core 3.1 solution (`BankSystem.sln` at the repo root) made of three independently deployed web apps:

- **BankSystem.Web** (`web/BankSystem.Web`) — the bank: MVC + Razor Pages (Identity under `Pages/Account`), feature areas `Cards`, `MoneyTransfers`, `Administration`.
- **CentralApi** (`web/CentralApi`) — a hub that connects multiple BankSystem instances and routes global transfers, card payments and direct payments between them.
- **DemoShop** (`demo/`) — sample merchant site that pays via CentralApi (direct and card payments).

Repo layout (mirrored by solution folders in `BankSystem.sln`): `core/` (shared libraries and the Models/Data/Services layers of BankSystem and CentralApi), `web/` (BankSystem.Web, CentralApi), `demo/` (DemoShop, all layers), `tests/`. `./rsa-keygen.sh [bits]` (openssl) generates RSA key pairs in the .NET `<RSAKeyValue>` XML format used in config. Setup and feature docs live in the GitHub wiki (https://github.com/banksystembg/BankSystem/wiki), linked from README.md.

## Commands

All projects target `netcoreapp3.1` / `netstandard2.1`. Newer SDKs build them, but running apps/tests needs the .NET Core 3.1 runtime (x64 only on macOS).

```bash
dotnet restore
dotnet build --no-restore
dotnet test                                                        # all tests (xUnit)
dotnet test tests/BankSystem.Services.Tests                        # one project
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
docker compose up --build   # BankSystem.Web :8080, DemoShop :8082 (db and api (CentralApi) are internal only)
```

Compose overrides connection strings and inter-service URLs through env vars (`Section__Key`); set `MSSQL_SA_PASSWORD` in `.env` to change the default SA password.

## Architecture

### Layering
Each app follows the same layered layout, split into separate projects per layer:
`*.Models` (EF entities) → `*.Data` (DbContext + migrations) → `*.Services.Models` (DTOs) → `*.Services` → web project. Every project sits directly under its top-level folder (no nesting): BankSystem and CentralApi layers in `core/`, DemoShop layers in `demo/`. `core/BankSystem.Common` is shared by BankSystem and CentralApi (config classes, crypto utils, email sender, AutoMapper setup).

### Convention-based DI
`web/BankSystem.Web/Infrastructure/Extensions/ServiceCollectionExtensions.cs` registers every class `Foo` that implements an interface named `IFoo` — scoped for the Services and Common assemblies, transient for the web assembly. New services need no manual registration as long as they follow the `Foo`/`IFoo` naming.

### Convention-based AutoMapper
`core/BankSystem.Common/AutoMapping/Profiles/DefaultProfile.cs` scans loaded assemblies whose name contains `BankSystem` or `CentralApi`:
- types implementing `IMapWith<T>` get a bidirectional map with `T`;
- types implementing `IHaveCustomMapping` configure their own maps.
Add mappings by implementing these interfaces on the DTO/view model rather than writing new profiles. Because assemblies load lazily, tests make sure the right assemblies are loaded before the profile is built.

### Inter-bank security protocol
All bank ↔ CentralApi traffic uses hybrid encryption + RSA signatures (`core/BankSystem.Common/Utils/SignatureVerificationUtil.cs`, `CryptographyExtensions.cs`, `RsaExtensions.cs`):
- Sender signs with its private key and encrypts an AES key/IV with the recipient's public key; the payload is a Base64 JSON `{ EncryptedKey, EncryptedIv, Data, Signature }`.
- Receivers use a `[DecryptAndVerifyRequest]` action filter (one in BankSystem.Web, one in CentralApi) that decrypts, verifies, and **replaces the action's first argument** with the decrypted JSON string, returning 403 on failure. API actions therefore take `[FromBody] string data` and deserialize it themselves.
- Bank API endpoints: `web/BankSystem.Web/Api/` (`ReceiveMoneyTransfers`, `CardPayments`). CentralApi endpoints: `ReceiveTransactions`, `CardPayments`, `Payments`.

### Configuration and multi-bank setup
- Bank identity comes from the `BankConfiguration` section (name, `UniqueIdentifier` = swift code, country, `First3CardDigits`, own RSA `Key`, `CentralApiPublicKey`, `CentralApiAddress`). It and `SendGridConfiguration` are validated in `PostConfigure`, so startup throws if a value is missing.
- CentralApi knows banks through its `Banks` table (`core/CentralApi.Models/Bank.cs`: public `ApiKey`, `ApiAddress`, `PaymentUrl`, `CardPaymentUrl`, `BankIdentificationCardNumbers`). It routes transfers by matching name + swift code + country, and card payments by the card's first 3 digits. A default bank is seeded in `web/CentralApi/Infrastructure/ApplicationBuilderExtensions.cs`.
- BankSystem.Web seeds the `test@test.com` user with an account and the Administrator role on startup (`Infrastructure/Extensions/ApplicationBuilderExtensions.cs`).

### Tests
Only the service layers are tested (`tests/BankSystem.Services.Tests`, `tests/CentralApi.Services.Tests`): xUnit + FluentAssertions + Moq, against an EF Core InMemory DB that is created fresh per access of `BaseTest.DatabaseInstance`, with a shared mapper from `Setup/TestSetup.cs`.
