from urllib.parse import parse_qs, urlparse

from conftest import KYC, sign_in
from src.security import COOKIE_NAME


def query_of(response) -> dict:
    return {key: values[0] for key, values in parse_qs(urlparse(response.headers["location"]).query).items()}


async def test_login_goes_to_keycloak(client):
    response = await client.get("/api/auth/login")

    assert response.status_code == 302
    assert response.headers["location"].startswith(f"{KYC}/protocol/openid-connect/auth")
    query = query_of(response)
    assert query["client_id"] == "shop"
    assert query["redirect_uri"] == "http://shop.test/api/auth/callback"
    assert query["code_challenge_method"] == "S256"
    assert "prompt" not in query


async def test_register_opens_the_keycloak_registration(client):
    response = await client.get("/api/auth/login?register=true")

    assert query_of(response)["prompt"] == "create"


async def test_callback_creates_the_user_and_comes_back(client, kyc, redis):
    response = await sign_in(client, kyc, email="new@abs.uz", name="New User")

    assert response.status_code == 303
    assert response.headers["location"] == "http://shop.test/cabinet"
    assert "httponly" in response.headers["set-cookie"].lower()
    user = (await client.get("/api/auth/me")).json()
    assert (user["email"], user["name"]) == ("new@abs.uz", "New User")


async def test_callback_returns_to_next(client, kyc):
    response = await sign_in(client, kyc, query="?next=/cabinet/products")

    assert response.headers["location"] == "http://shop.test/cabinet/products"


async def test_callback_stays_on_the_site(client, kyc):
    response = await sign_in(client, kyc, query="?next=//evil.test")

    assert response.headers["location"] == "http://shop.test/cabinet"


async def test_callback_finishes_the_purchase(client, kyc, product):
    response = await sign_in(client, kyc, query=f"?buy={product.id}")

    assert "/cabinet/orders?pay=" in response.headers["location"]
    assert len((await client.get("/api/orders")).json()) == 1


async def test_second_login_keeps_the_user_and_the_profile(signed_in, kyc):
    await signed_in.put("/api/auth/me", json={"name": "Edited"})
    signed_in.cookies.clear()

    await sign_in(signed_in, kyc, name="From Keycloak")

    assert (await signed_in.get("/api/auth/me")).json()["name"] == "Edited"


async def test_state_works_only_once(client, kyc):
    location = (await client.get("/api/auth/login")).headers["location"]
    state = query_of_url(location)["state"]
    nonce = query_of_url(location)["nonce"]
    kyc["claims"] = {"iss": KYC, "aud": "shop", "nonce": nonce, "exp": 9999999999, "email": "a@abs.uz"}

    first = await client.get("/api/auth/callback", params={"state": state, "code": "code"})
    second = await client.get("/api/auth/callback", params={"state": state, "code": "code"})

    assert (first.status_code, second.status_code) == (303, 400)


def query_of_url(url: str) -> dict:
    return {key: values[0] for key, values in parse_qs(urlparse(url).query).items()}


async def test_unknown_state_is_refused(client):
    response = await client.get("/api/auth/callback", params={"state": "unknown", "code": "code"})

    assert response.status_code == 400


async def test_wrong_nonce_is_refused(client, kyc):
    assert (await sign_in(client, kyc, nonce="other")).status_code == 400


async def test_token_of_another_client_is_refused(client, kyc):
    assert (await sign_in(client, kyc, aud="bank")).status_code == 400


async def test_token_of_another_issuer_is_refused(client, kyc):
    assert (await sign_in(client, kyc, iss="http://evil.test")).status_code == 400


async def test_expired_token_is_refused(client, kyc):
    assert (await sign_in(client, kyc, exp=1)).status_code == 400


async def test_refused_code_does_not_sign_in(client, kyc):
    kyc["status"] = 400

    assert (await sign_in(client, kyc)).status_code == 502
    assert (await client.get("/api/auth/me")).status_code == 401


async def test_cancelled_login_goes_back_to_the_site(client):
    state = query_of(await client.get("/api/auth/login"))["state"]

    response = await client.get("/api/auth/callback", params={"state": state, "error": "access_denied"})

    assert response.headers["location"] == "http://shop.test/"


async def test_logout_ends_the_session_and_goes_to_keycloak(signed_in, redis):
    response = await signed_in.post("/api/auth/logout")

    assert response.status_code == 200
    assert response.json()["url"].startswith(f"{KYC}/protocol/openid-connect/logout?id_token_hint=")
    assert await redis.dbsize() == 0
    assert (await signed_in.get("/api/auth/me")).status_code == 401


async def test_logout_without_a_session_stays_on_the_site(client):
    assert (await client.post("/api/auth/logout")).json()["url"] == "http://shop.test/"


async def test_session_cookie_is_set(signed_in):
    assert COOKIE_NAME in signed_in.cookies


async def test_me_needs_a_session(client):
    assert (await client.get("/api/auth/me")).status_code == 401


async def test_new_user_has_an_empty_profile(signed_in):
    user = (await signed_in.get("/api/auth/me")).json()

    assert (user["name"], user["phone"], user["address"]) == ("Buyer", "", "")


async def test_profile_can_be_edited(signed_in):
    profile = {"name": "New Name", "phone": "+998 90 123 45 67", "address": "Tashkent"}

    response = await signed_in.put("/api/auth/me", json=profile)

    assert response.status_code == 200
    assert (await signed_in.get("/api/auth/me")).json() | profile == response.json()


async def test_profile_needs_a_session(client):
    assert (await client.put("/api/auth/me", json={"name": "A"})).status_code == 401


async def test_profile_needs_a_name(signed_in):
    assert (await signed_in.put("/api/auth/me", json={"name": ""})).status_code == 422
