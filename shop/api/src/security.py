import secrets

import bcrypt
from fastapi import Cookie, Depends, HTTPException, Response, status
from redis.asyncio import Redis
from sqlalchemy.ext.asyncio import AsyncSession

from src.cache import get_redis
from src.config import get_settings
from src.db import get_db
from src.models import User

COOKIE_NAME = "shop_session"


def hash_password(password: str) -> str:
    return bcrypt.hashpw(password.encode(), bcrypt.gensalt()).decode()


def verify_password(password: str, password_hash: str) -> bool:
    return bcrypt.checkpw(password.encode(), password_hash.encode())


def _key(token: str) -> str:
    return f"session:{token}"


async def start_session(response: Response, redis: Redis, user_id: str) -> None:
    token = secrets.token_urlsafe(32)
    lifetime = get_settings().session_days * 86400
    await redis.set(_key(token), user_id, ex=lifetime)
    response.set_cookie(COOKIE_NAME, token, max_age=lifetime, httponly=True, samesite="lax", path="/")


async def end_session(response: Response, redis: Redis, token: str | None) -> None:
    if token:
        await redis.delete(_key(token))
    response.delete_cookie(COOKIE_NAME, path="/")


async def current_user(
    token: str | None = Cookie(default=None, alias=COOKIE_NAME),
    redis: Redis = Depends(get_redis),
    db: AsyncSession = Depends(get_db),
) -> User:
    user_id = await redis.get(_key(token)) if token else None
    user = await db.get(User, user_id) if user_id else None
    if user is None:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "Not signed in")
    return user
