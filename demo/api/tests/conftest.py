import os
from collections.abc import AsyncIterator

os.environ["DATABASE_URL"] = "sqlite+aiosqlite:///:memory:"

import base64

import fakeredis.aioredis
import httpx
import pytest
from cryptography.hazmat.primitives.asymmetric import rsa
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker, create_async_engine
from sqlalchemy.pool import StaticPool

from src.cache import get_redis
from src.config import Settings, get_settings
from src.db import Base, get_db
from src.main import app
from src.models import Product
from src.routers.orders import get_http_client
from src.seed import DEMO_PASSWORD
from src.storage import get_storage


def _encode(value: int) -> str:
    return base64.b64encode(value.to_bytes((value.bit_length() + 7) // 8, "big")).decode()


class Keys:
    """An RSA key pair in the .NET XML format."""

    def __init__(self) -> None:
        self.key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
        numbers = self.key.private_numbers()
        public = numbers.public_numbers
        parts = f"<Modulus>{_encode(public.n)}</Modulus><Exponent>{_encode(public.e)}</Exponent>"
        self.public = f"<RSAKeyValue>{parts}</RSAKeyValue>"
        self.private = (
            f"<RSAKeyValue>{parts}<P>{_encode(numbers.p)}</P><Q>{_encode(numbers.q)}</Q>"
            f"<DP>{_encode(numbers.dmp1)}</DP><DQ>{_encode(numbers.dmq1)}</DQ>"
            f"<InverseQ>{_encode(numbers.iqmp)}</InverseQ><D>{_encode(numbers.d)}</D></RSAKeyValue>"
        )


@pytest.fixture(scope="session")
def site_keys() -> Keys:
    return Keys()


@pytest.fixture(scope="session")
def central_keys() -> Keys:
    return Keys()


@pytest.fixture(scope="session")
def bank_keys() -> Keys:
    return Keys()


@pytest.fixture
def settings(site_keys: Keys, central_keys: Keys) -> Settings:
    return Settings(
        site_key=site_keys.private,
        central_api_public_key=central_keys.public,
        site_url="http://shop.test/",
        central_api_payment_url="http://central.test/pay",
        central_api_card_payments_url="http://central.test/api/CardPayments",
        destination_bank_name="Bank system",
        destination_bank_country="Uzbekistan",
        destination_bank_swift_code="ABC",
        destination_bank_account_unique_id="ABCJ98131785",
        recipient_name="ABS Admin",
    )


@pytest.fixture
def central_api() -> dict:
    """What the fake CentralApi received, and the status it answers with."""
    return {"status": 200, "requests": []}


@pytest.fixture
async def db_factory() -> AsyncIterator[async_sessionmaker[AsyncSession]]:
    engine = create_async_engine("sqlite+aiosqlite:///:memory:", poolclass=StaticPool)
    async with engine.begin() as connection:
        await connection.run_sync(Base.metadata.create_all)
    yield async_sessionmaker(engine, expire_on_commit=False)
    await engine.dispose()


@pytest.fixture
async def redis() -> AsyncIterator[fakeredis.aioredis.FakeRedis]:
    client = fakeredis.aioredis.FakeRedis(decode_responses=True)
    yield client
    await client.aclose()


class FakeStorage:
    def __init__(self) -> None:
        self.objects: dict[str, tuple[bytes, str]] = {}

    def put(self, key: str, data: bytes, content_type: str) -> None:
        self.objects[key] = (data, content_type)

    def get(self, key: str) -> tuple[bytes, str] | None:
        return self.objects.get(key)


@pytest.fixture
def storage() -> FakeStorage:
    return FakeStorage()


@pytest.fixture
async def client(db_factory, redis, settings, central_api, storage) -> AsyncIterator[httpx.AsyncClient]:
    async def db():
        async with db_factory() as session:
            yield session

    async def redis_client():
        yield redis

    def handler(request: httpx.Request) -> httpx.Response:
        central_api["requests"].append(request)
        return httpx.Response(central_api["status"])

    async def http_client():
        async with httpx.AsyncClient(transport=httpx.MockTransport(handler)) as outgoing:
            yield outgoing

    app.dependency_overrides.update(
        {
            get_db: db,
            get_redis: redis_client,
            get_settings: lambda: settings,
            get_storage: lambda: storage,
            get_http_client: http_client,
        }
    )
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://shop.test") as test_client:
        yield test_client
    app.dependency_overrides.clear()


@pytest.fixture
async def product(db_factory) -> Product:
    async with db_factory() as session:
        item = Product(name="Laptop", price=8900000, image_url="/img/laptop.svg")
        session.add(item)
        await session.commit()
        return item


@pytest.fixture
async def signed_in(client: httpx.AsyncClient) -> httpx.AsyncClient:
    response = await client.post(
        "/api/auth/register", json={"name": "Buyer", "email": "buyer@abs.uz", "password": DEMO_PASSWORD}
    )
    assert response.status_code == 201
    return client


