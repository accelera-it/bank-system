# Core

The bank and the hub that connects banks.

| Component | Description
| - | -
| **BankSystem.Web** | The bank: web UI plus API endpoints that receive transfers and card payments
| **CentralApi** | Connects banks together; routes global transfers, card payments and direct payments
| **nginx** | The only public entry, on port 8081: `/api/` goes to Ocelot, everything else to BankSystem.Web
| **Ocelot** | Gateway for every call to CentralApi

## Features

- **Bank accounts** – owner, balance, transaction history
- **Money transfers** – internal (same bank) and global (between banks via CentralApi)
- **Cards** – issue payment cards and use them for online purchases
- **Direct payments** – pay on third-party websites straight from a bank account, without card details
- **Administration** – bank administration area
- **User settings** – account and security settings

## Structure

| Path | Contents
| - | -
| `app/` | `BankSystem.Common` and the Models / Data / Services layers and web projects of BankSystem and CentralApi
| `app/CentralApi.Gateway/` | Ocelot gateway for CentralApi; routes are in `ocelot.json`
| `proxy/` | nginx, the public entry for both apps
| `tests/` | Dockerfile of the test run; the xUnit test projects are in `app/`
| `data/oracle/` | Oracle image, with one schema per app
| `data/redis/` | Redis image, for the bank's login sessions and cache
| `docker-compose.yml` | Oracle, Redis, CentralApi, BankSystem.Web, Ocelot and nginx
| `docker-compose.test.yml` | The four test projects, one service each
| `bank.env` | Environment of `docker-compose.yml`

## Running

### With Docker

Run from the repository root. Start core before `demo`, because it creates the `bank_lan` network the shop joins.

```bash
docker compose -f bank/docker-compose.yml up -d --build
```

| Service | Address
|-|-
| **BankSystem.Web** | <http://localhost:8081>
| **CentralApi** | <http://localhost:8081/api>

CentralApi serves Swagger at <http://localhost:8081/api/swagger/index.html> in the Development environment. It lists the endpoints only: the API bodies are encrypted and signed, so "Try it out" returns 403.

Only nginx publishes a port. CentralApi's `/pay` page is public as `/api/pay`, because the bank has its own `/pay`. Everything else is reachable inside the Docker network only. The bank and the shop call CentralApi through Ocelot; CentralApi calls the bank's API on `web` directly. Passwords are in `bank.env`. A new database password needs a fresh volume, because Oracle keeps the one it was created with.

### Without Docker

Requires the .NET 10 SDK and Oracle. Run these from `bank/`, which holds `BankSystem.sln`. Settings and RSA keys are environment variables, listed in `bank.env`; each app's `appsettings.json` only holds logging defaults. Databases are migrated automatically on startup. Without `ConnectionStrings:Redis`, login sessions and the cache are kept in memory.

```bash
dotnet run --project app/CentralApi
dotnet run --project app/BankSystem.Web
```

### Tests

The tests run on EF Core InMemory and need no database.

```bash
dotnet test app/BankSystem.UnitTests
docker compose -f bank/docker-compose.test.yml up --build --abort-on-container-failure
docker compose -f bank/docker-compose.test.yml run --rm --build api-unit   # one project: {web,api}-{unit,integration}
```

### Demo account

On first start the bank creates a demo user with a 10,000,000 UZS account:

| Email | Password
|-|-
| <admin@abs.uz> | 123456

## Configuration

Each bank is configured through the `BankConfiguration__*` variables in `bank.env`: bank name, unique identifier (bank code), country, first 3 card digits, its own RSA private key and the CentralApi public key and address. The app refuses to start if any value is missing.

Banks known to CentralApi are stored in its database (name, code, country, public key and API addresses).

### Generating RSA keys

Keys use the .NET `<RSAKeyValue>` XML format. To generate a new key pair (requires `openssl`), run from the repository root:

```bash
./rsa-keygen.sh        # 4096-bit
./rsa-keygen.sh 2048
```
