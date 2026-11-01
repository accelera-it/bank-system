# KYC

Keycloak, the identity system for the bank and the shop. Both are registered as clients of the `abs` realm, but they still use their own login pages: moving them to Keycloak is not done yet.

## Structure

| Path | Contents
| - | -
| `app/` | Keycloak image and the `abs` realm in `realms/abs-realm.json`
| `data/pg/` | PostgreSQL image, the database of the running stack
| `data/redis/` | Redis image, for the session scenarios in the tests
| `proxy/` | nginx, the public entry
| `app/tests/` | Login, register, token and session tests (Java, JUnit)
| `docker-compose.yml` | PostgreSQL, Keycloak and nginx
| `docker-compose.test.yml` | The tests, with Keycloak on its embedded H2 database and Redis
| `kyc.env.example` | Settings of `docker-compose.yml`, without the secrets

## Running

Run from the repository root. `kyc.env` holds secrets and is not in git: copy `kyc.env.example` to `kyc.env` and fill in the empty values.

```bash
docker compose -f kyc/docker-compose.yml up -d --build
```

Keycloak is at <http://localhost:8080>; inside its container it listens on port 8080 too. The realm is imported on first start, so changes to `abs-realm.json` need a fresh volume.

| User | Password | Where
|-|-|-
| `admin` | 123456 | Keycloak root user, `master` realm
| `demo@abs.uz` | 123456 | Default user, `abs` realm

## Tests

```bash
docker compose -f kyc/docker-compose.test.yml up --build --abort-on-container-failure --exit-code-from kyc-tests kyc-tests
docker compose -f kyc/docker-compose.test.yml down -v
```
