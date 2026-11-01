# BankSystem

**BankSystem** is a web banking application built with ASP.NET Core. Users can open bank accounts, transfer money, issue payment cards and pay online with a card or directly from their account.

Several independent BankSystem instances can be connected through **CentralApi**, which securely routes transfers and payments between banks.

## Features

- **Bank accounts** – owner, balance, transaction history
- **Money transfers** – internal (same bank) and global (between banks via CentralApi)
- **Cards** – issue payment cards and use them for online purchases
- **Direct payments** – pay on third-party websites straight from a bank account, without card details
- **Administration** – bank administration area
- **User settings** – account and security settings

## How it works

```
demo (DemoShop) ──► api (CentralApi) ──► web (BankSystem.Web)
                          │
                          └──► other BankSystem instances
```

| Component | Description
| - | -
| **BankSystem.Web** | The bank: web UI plus API endpoints that receive transfers and card payments
| **CentralApi** | Connects banks together; routes global transfers, card payments and direct payments
| **DemoShop** | Sample online shop that accepts card and direct payments

All traffic between components is encrypted and signed (RSA + AES hybrid encryption).

## Repository structure

| Folder | Contents
| - | -
| `core/` | BankSystem and CentralApi: `src/`, `tests/`, `data/` (Oracle, Redis), compose files, `bank.env`
| `demo/` | DemoShop: `src/`, `tests/`, `data/` (PostgreSQL, Redis), compose files, `shop.env`

All projects are in `BankSystem.sln` at the repository root.

## Running locally

### With Docker

```bash
docker compose -f core/docker-compose.yml up -d --build   # start first, creates the bank_lan network
docker compose -f demo/docker-compose.yml up -d --build
```

| Service | Address
|-|-
| **BankSystem.Web** | <http://localhost:8081>
| **CentralApi** | <http://localhost:8080>
| **DemoShop** | <http://localhost:8082>

Oracle, PostgreSQL and Redis (built from each side's `data/` folder) are only reachable inside the Docker network. All containers use the `Asia/Tashkent` timezone. Passwords are in `core/bank.env` and `demo/shop.env`; a new password needs a fresh volume, because the databases keep the one they were created with.

### Without Docker

Requires the .NET 10 SDK, Oracle for BankSystem and CentralApi, and PostgreSQL for DemoShop (connection strings are in each app's `appsettings.json`). Databases are migrated automatically on startup.

```bash
dotnet build
dotnet test
dotnet run --project core/src/CentralApi
dotnet run --project core/src/BankSystem.Web
dotnet run --project demo/src/DemoShop.Web
```

### Tests

`dotnet test` runs all six test projects without a database. To run them in Docker, on EF Core InMemory with no database (services `web-`, `api-` and `demo-` + `unit` or `integration`):

```bash
docker compose -f core/docker-compose.test.yml up --build --abort-on-container-failure
docker compose -f demo/docker-compose.test.yml up --build --abort-on-container-failure
docker compose -f core/docker-compose.test.yml run --rm --build api-unit   # one project
```

### Demo account

On first start the bank creates a demo user with a 10,000,000 UZS account:

| Email | Password
|-|-
| <admin@abs.uz> | 123456

## Configuration

Each bank is configured through the `BankConfiguration` section of `core/src/BankSystem.Web/appsettings.json`: bank name, unique identifier (bank code), country, first 3 card digits, its own RSA private key and the CentralApi public key and address. The app refuses to start if any value is missing.

Banks known to CentralApi are stored in its database (name, code, country, public key and API addresses).

### Generating RSA keys

Keys use the .NET `<RSAKeyValue>` XML format. To generate a new key pair (requires `openssl`):

```bash
./rsa-keygen.sh        # 4096-bit
./rsa-keygen.sh 2048
```

## Tech stack

- C#, ASP.NET Core 10 (MVC, Razor Pages, Web API)
- Entity Framework Core, Oracle, PostgreSQL
- Redis (login sessions, cache)
- ASP.NET Core Identity
- Mapster
- jQuery, Bootstrap
- xUnit, Moq, FluentAssertions
- Docker, Docker Compose

## Acknowledgements

Based on [banksystembg/BankSystem](https://github.com/banksystembg/BankSystem).
