# Shop

Sample online shop, DemoShop, that accepts card and direct payments through CentralApi.

| Component | Description
| - | -
| **Shop API** | FastAPI middleware: accounts, products, orders and the calls to CentralApi
| **Sign-in** | Login, register, Edit profile and Change password run at Keycloak and come back, see [kyc/README.md](../kyc/README.md)
| **UI** | One React, TypeScript and Vite app: the public shop in Tailwind CSS and, under `/cabinet`, the customer cabinet in Ant Design

## Structure

| Path | Contents
| - | -
| `api/` | Shop API; its tests are in `api/tests/` (pytest)
| `app/` | Shop and cabinet; the UI tests are in `app/tests/` (Playwright)
| `data/pg/` | PostgreSQL image
| `data/redis/` | Redis image, for login sessions and cache
| `data/minio/` | MinIO image, for product images
| `docker-compose.yml` | PostgreSQL, Redis, MinIO, the API and the UI
| `docker-compose.test.yml` | The API and UI tests, with real Keycloak and a CentralApi stub
| `shop.env` | Environment of `docker-compose.yml`

## Running

Run from the repository root. Start `kyc` and `bank` first: the API reaches Keycloak on `kyc_lan` and CentralApi on `bank_lan`.

```bash
docker compose -f shop/docker-compose.yml up -d --build
```

| Service | Address
|-|-
| **Shop** | <http://localhost:8082>
| **Cabinet** | <http://localhost:8082/cabinet>

The API, PostgreSQL, Redis and MinIO are only reachable inside the Docker network. Passwords and RSA keys are in `shop.env`. A new database password needs a fresh volume, because PostgreSQL keeps the one it was created with.

### Tests

```bash
docker compose -f shop/docker-compose.test.yml run --rm --build shop_api_tests
docker compose -f shop/docker-compose.test.yml up --build --abort-on-container-failure --exit-code-from shop_ui_tests shop_ui_tests
docker compose -f shop/docker-compose.test.yml down -v
```

The API tests use SQLite and an in-memory Redis. The UI tests run a real browser against the whole stack and a real Keycloak, with `tests/central-stub.ts` in place of CentralApi.

### Demo account

On first start the API creates a laptop product and the default user. Sign in at Keycloak; anyone else gets a shop user on the first sign-in:

| Email | Password
|-|-
| <demo@abs.uz> | 123456
