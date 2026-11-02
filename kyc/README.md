# KYC

Keycloak, the identity system for the bank and the shop. Both are clients of the `abs` realm and have no login or register pages of their own: users come here and go back.

## Structure

| Path | Contents
| - | -
| `app/` | Keycloak image, the `abs` realm in `realms/abs-realm.json` and the `abs` login theme in `themes/`, in the colors of the bank and the shop
| `proxy/` | nginx, the public entry
| `data/pg/` | Postgres image
| `app/provider/` | Keycloak provider that checks the current password in Change password
| `app/setup.sh` | Entry point of `kyc_app`: starts Keycloak, makes the root user permanent and sets the `master` realm: no Remember me, no login with email, SSL not required
| `app/tests/` | Login, register, token and session tests (Java, JUnit)
| `docker-compose.yml` | Postgres, Keycloak and nginx
| `docker-compose.test.yml` | The tests, with Keycloak on a throwaway Postgres
| `kyc.env` | Keycloak settings of `docker-compose.yml`
| `client.env` | Client secrets, read by kyc, bank and shop

Login asks for email and password. Register asks for Full Name, email, password and its confirmation, and the button waits for the agreement checkbox. Edit profile shows Full Name, Phone and Address, with the email read-only, and Change password asks for the current password, the new one and its confirmation. After either one the user signs in again, in the bank and the shop.

## Running

Run from the repository root. Start it before `bank` and `shop`: they join its `kyc_lan` network.

```bash
docker compose -f kyc/docker-compose.yml up -d --build
```

Keycloak keeps its data in Postgres (`kyc_pg`), in the `pg_data` volume. Keycloak is at <http://localhost:8080>; inside its container it listens on port 8080 too. The realm is imported on first start, so changes to `abs-realm.json` need a fresh volume.

| User | Password | Where
|-|-|-
| `admin` | 123456 | Keycloak root user, `master` realm
| `demo@abs.uz` | 123456 | Default user, `abs` realm

## Tests

```bash
docker compose -f kyc/docker-compose.test.yml up --build --abort-on-container-failure --exit-code-from kyc_tests kyc_tests
docker compose -f kyc/docker-compose.test.yml down -v
```
