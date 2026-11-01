from src.routers.products import CACHE_KEY

NEW_PRODUCT = {"name": "Phone", "price": "1500000.50", "image_url": "/img/phone.svg"}


async def test_list_is_public_and_cached_in_redis(client, product, redis):
    response = await client.get("/api/products")

    assert response.status_code == 200
    assert [p["name"] for p in response.json()] == ["Laptop"]
    assert await redis.exists(CACHE_KEY)


async def test_list_is_served_from_the_cache(client, redis):
    await redis.set(CACHE_KEY, '[{"id": "1", "name": "Cached", "price": 1, "image_url": "/x"}]')

    response = await client.get("/api/products")

    assert [p["name"] for p in response.json()] == ["Cached"]


async def test_create_needs_a_session(client):
    assert (await client.post("/api/products", json=NEW_PRODUCT)).status_code == 401


async def test_create_clears_the_cache(signed_in, redis):
    await signed_in.get("/api/products")

    response = await signed_in.post("/api/products", json=NEW_PRODUCT)

    assert response.status_code == 201
    assert not await redis.exists(CACHE_KEY)
    assert [p["name"] for p in (await signed_in.get("/api/products")).json()] == ["Phone"]


async def test_create_rejects_a_price_of_zero(signed_in):
    response = await signed_in.post("/api/products", json={**NEW_PRODUCT, "price": "0"})

    assert response.status_code == 422


async def test_unknown_product_is_not_found(client):
    assert (await client.get("/api/products/missing")).status_code == 404


PNG = b"\x89PNG\r\n\x1a\n" + b"0" * 32


async def upload(client, data: bytes):
    return await client.post("/api/products/images", files={"file": ("image.png", data, "image/png")})


async def test_upload_needs_a_session(client):
    assert (await upload(client, PNG)).status_code == 401


async def test_uploaded_image_is_served_to_guests(signed_in, storage):
    url = (await upload(signed_in, PNG)).json()["url"]
    signed_in.cookies.clear()

    response = await signed_in.get(url)

    assert response.status_code == 200
    assert response.content == PNG
    assert response.headers["content-type"] == "image/png"
    assert response.headers["x-content-type-options"] == "nosniff"
    assert len(storage.objects) == 1


async def test_upload_rejects_what_is_not_an_image(signed_in, storage):
    response = await upload(signed_in, b"<svg onload='alert(1)'></svg>")

    assert response.status_code == 415
    assert not storage.objects


async def test_upload_rejects_a_big_image(signed_in):
    response = await upload(signed_in, PNG + b"0" * (5 * 1024 * 1024))

    assert response.status_code == 413


async def test_missing_image_is_not_found(client):
    assert (await client.get(f"/api/products/images/{'0' * 32}.png")).status_code == 404


async def test_image_key_cannot_leave_the_bucket(client):
    assert (await client.get("/api/products/images/..%2Fsecret")).status_code in (404, 422)
