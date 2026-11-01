# Bank

The bank and the hub that connects banks.

| Component | Description
| - | -
| **BankSystem.Web** | The bank: web UI plus API endpoints that receive transfers and card payments
| **CentralApi** | Connects banks together; routes global transfers, card payments and direct payments
| **nginx** | The only public entry, on port 8081: `/api/` goes to Kong and everything else to BankSystem.Web; limits `/api/` to 60 calls a minute per client IP
| **Kong** | Gateway for every API call, to CentralApi and to the bank; DB-less, with its routes in `gateway/kong.yml`

## Features

- **Bank accounts** – owner, balance, transaction history
- **Money transfers** – internal (same bank) and global (between banks via CentralApi)
- **Cards** – issue payment cards and use them for online purchases
- **Direct payments** – pay on third-party websites straight from a bank account, without card details
- **Administration** – bank administration area
- **Sign-in** – login, register, Edit profile and Change password run at Keycloak, see [kyc/README.md](../kyc/README.md)

## Structure

| Path | Contents
| - | -
| `app/src/` | The Models / Data / Services layers and web projects of BankSystem and CentralApi
| `app/core/` | `BankSystem.Common`, used by both apps
| `gateway/` | Kong gateway; routes are in `kong.yml`
| `proxy/` | nginx, the public entry for both apps
| `app/tests/` | Unit and integration tests (xUnit) and their Dockerfile
| `data/mssql/` | SQL Server image; each app creates its own database
| `data/redis/` | Redis image, for the bank's login sessions and cache
| `docker-compose.yml` | SQL Server, Redis, CentralApi, BankSystem.Web, Kong and nginx
| `docker-compose.test.yml` | The four test projects, one service each
| `bank.env` | Environment of `docker-compose.yml`

## Running

### With Docker

Run from the repository root. Start `kyc` first, because the bank joins its `kyc_lan` network, and the bank before `shop`, which joins `bank_lan`.

```bash
docker compose -f bank/docker-compose.yml up -d --build
```

| Service | Address
|-|-
| **BankSystem.Web** | <http://localhost:8081>
| **CentralApi** | <http://localhost:8081/api>

CentralApi serves Swagger at <http://localhost:8081/api/swagger/index.html> in the Development environment. It lists the endpoints only: the API bodies are encrypted and signed, so "Try it out" returns 403.

Only nginx publishes a port. Above the limit in `proxy/nginx.conf`, public `/api/` calls get `429`. CentralApi's `/pay` page is public as `/api/pay`, because the bank has its own `/pay`. Everything else is reachable inside the Docker network only.

The bank and the shop call CentralApi through Kong (`bank_api`), and CentralApi reaches the bank's API through it at `/bank/api/`, which nginx does not publish. `kyc/client.env` holds the bank's client secret. SQL Server runs as `linux/amd64`, so it is slower under emulation on Apple Silicon.

### Without Docker

Requires the .NET 10 SDK and SQL Server. Run these from `bank/`, which holds `BankSystem.sln`. Settings and RSA keys are environment variables, listed in `bank.env`; each app's `appsettings.json` only holds logging defaults. Databases are migrated automatically on startup. Without `ConnectionStrings:Redis`, login sessions and the cache are kept in memory.

```bash
dotnet run --project app/src/CentralApi
dotnet run --project app/src/BankSystem.Web
```

### Tests

The tests run on EF Core InMemory and need no database.

```bash
dotnet test app/tests/BankSystem.UnitTests
docker compose -f bank/docker-compose.test.yml up --build --abort-on-container-failure
docker compose -f bank/docker-compose.test.yml run --rm --build bank_api_unit   # one project: bank_{web,api}_{unit,integration}
```

### Demo account

On first start the bank creates the default user, an administrator with a 10,000,000 UZS account. Sign in at Keycloak:

| Email | Password
|-|-
| <demo@abs.uz> | 123456

Anyone else gets their bank user on the first sign-in.

## Configuration

Each bank is configured through the `BankConfiguration__*` variables in `bank.env`: bank name, unique identifier (bank code), country, first 3 card digits, its own RSA private key and the CentralApi public key and address. The app refuses to start if any value is missing.

Banks known to CentralApi are stored in its database (name, code, country, public key and API addresses).

### Generating RSA keys

Keys use the .NET `<RSAKeyValue>` XML format. To generate a new key pair (requires `openssl`), run from the repository root:

```bash
docs/rsa-keygen.sh        # 4096-bit
docs/rsa-keygen.sh 2048
```
