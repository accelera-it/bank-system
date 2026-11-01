# DemoShop

Sample online shop that accepts card and direct payments through CentralApi.

## Structure

| Path | Contents
| - | -
| `src/` | The Models / Data / Services layers and `DemoShop.Web`
| `tests/` | Unit and integration tests (xUnit)
| `data/pg/` | PostgreSQL image
| `data/redis/` | Redis image, for the shop's login sessions and cache
| `docker-compose.yml` | PostgreSQL, Redis and DemoShop.Web
| `docker-compose.test.yml` | The two test projects, one service each
| `shop.env` | Environment of `docker-compose.yml`

## Running

### With Docker

Run from the repository root. Start `core` first: the shop reaches CentralApi through the `bank_lan` network that `core` creates.

```bash
docker compose -f demo/docker-compose.yml up -d --build
```

The shop is at <http://localhost:8082>. PostgreSQL and Redis are only reachable inside the Docker network. Passwords are in `shop.env`. A new database password needs a fresh volume, because PostgreSQL keeps the one it was created with.

### Without Docker

Requires the .NET 10 SDK and PostgreSQL (the connection string is in `src/DemoShop.Web/appsettings.json`). The database is migrated automatically on startup. Without `ConnectionStrings:Redis`, login sessions and the cache are kept in memory.

```bash
dotnet run --project demo/src/DemoShop.Web
```

### Tests

The tests run on EF Core InMemory and need no database.

```bash
dotnet test demo/tests/DemoShop.UnitTests
docker compose -f demo/docker-compose.test.yml up --build --abort-on-container-failure
docker compose -f demo/docker-compose.test.yml run --rm --build demo-unit   # one project: demo-{unit,integration}
```

### Demo account

On first start the shop creates a demo user and a laptop product:

| Email | Password
|-|-
| <demo@abs.uz> | 123456
