from src.security import COOKIE_NAME


async def test_register_signs_the_user_in(client, redis):
    response = await client.post("/api/auth/register", json={"name": "New", "email": "New@Abs.uz", "password": "123456"})

    assert response.status_code == 201
    assert response.json()["email"] == "new@abs.uz"
    assert "httponly" in response.headers["set-cookie"].lower()
    assert await redis.dbsize() == 1
    assert (await client.get("/api/auth/me")).status_code == 200


async def test_register_rejects_a_taken_email(signed_in):
    response = await signed_in.post("/api/auth/register", json={"name": "New", "email": "buyer@abs.uz", "password": "123456"})

    assert response.status_code == 409


async def test_register_rejects_a_short_password(client):
    response = await client.post("/api/auth/register", json={"name": "New", "email": "a@abs.uz", "password": "123"})

    assert response.status_code == 422


async def test_login_with_a_wrong_password_is_refused(signed_in):
    signed_in.cookies.clear()

    response = await signed_in.post("/api/auth/login", json={"email": "buyer@abs.uz", "password": "wrong-one"})

    assert response.status_code == 401


async def test_login_with_the_right_password_works(signed_in):
    signed_in.cookies.clear()

    response = await signed_in.post("/api/auth/login", json={"email": "buyer@abs.uz", "password": "123456"})

    assert response.status_code == 200
    assert COOKIE_NAME in response.cookies


async def test_logout_removes_the_session_from_redis(signed_in, redis):
    response = await signed_in.post("/api/auth/logout")

    assert response.status_code == 204
    assert await redis.dbsize() == 0
    assert (await signed_in.get("/api/auth/me")).status_code == 401


async def test_me_needs_a_session(client):
    assert (await client.get("/api/auth/me")).status_code == 401


async def test_register_needs_a_name(client):
    response = await client.post("/api/auth/register", json={"name": " ", "email": "a@abs.uz", "password": "123456"})

    assert response.status_code == 422


async def test_new_user_keeps_the_name_and_has_an_empty_profile(signed_in):
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
