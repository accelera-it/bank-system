import asyncio
from decimal import Decimal

from sqlalchemy import select, text

from src.db import Base, engine, session_factory
from src.models import Product, User
from src.security import hash_password
from src.storage import get_storage

DEMO_EMAIL = "demo@abs.uz"
DEMO_PASSWORD = "123456"
DEMO_NAME = "Demo User"
# create_all leaves existing tables alone, so databases from before these columns need them added.
USER_COLUMNS = ("name VARCHAR(100)", "phone VARCHAR(30)", "address VARCHAR(300)")
PRODUCTS = [
    ('Laptop 14" Core i7, 16 GB, 512 GB SSD', Decimal("8900000"), "/img/products/laptop.svg"),
]


async def seed() -> None:
    async with engine.begin() as connection:
        await connection.run_sync(Base.metadata.create_all)
        for column in USER_COLUMNS:
            await connection.execute(text(f"ALTER TABLE users ADD COLUMN IF NOT EXISTS {column} NOT NULL DEFAULT ''"))

    async with session_factory() as db:
        if await db.scalar(select(User).where(User.email == DEMO_EMAIL)) is None:
            db.add(User(email=DEMO_EMAIL, name=DEMO_NAME, password_hash=hash_password(DEMO_PASSWORD)))

        for name, price, image_url in PRODUCTS:
            if await db.scalar(select(Product).where(Product.name == name)) is None:
                db.add(Product(name=name, price=price, image_url=image_url))

        await db.commit()
    await engine.dispose()
    get_storage().ensure_bucket()


if __name__ == "__main__":
    asyncio.run(seed())
