import base64
import json
import secrets
import time

import httpx
from fastapi import Cookie, Depends, HTTPException, Response, status
from redis.asyncio import Redis
from sqlalchemy.ext.asyncio import AsyncSession

from src.cache import get_redis
from src.config import Settings, get_settings
from src.db import get_db
from src.http_client import get_http_client
from src.models import User

COOKIE_NAME = "shop_session"


def _key(token: str) -> str:
    return f"session:{token}"


def _tokens_key(token: str) -> str:
    return f"session_tokens:{token}"


def claims_of(id_token: str) -> dict:
    """The token comes straight from Keycloak over the back channel, so its claims are read, not re-verified."""
    payload = id_token.split(".")[1]
    return json.loads(base64.urlsafe_b64decode(payload + "=" * (-len(payload) % 4)))


def session_tokens(answer: dict) -> str:
    return json.dumps(
        {
            "id_token": answer["id_token"],
            "refresh_token": answer["refresh_token"],
            "expires_at": time.time() + answer["expires_in"],
        }
    )


async def start_session(response: Response, redis: Redis, user_id: str, tokens: dict) -> None:
    token = secrets.token_urlsafe(32)
    lifetime = get_settings().session_days * 86400
    await redis.set(_key(token), user_id, ex=lifetime)
    await redis.set(_tokens_key(token), session_tokens(tokens), ex=lifetime)
    response.set_cookie(COOKIE_NAME, token, max_age=lifetime, httponly=True, samesite="lax", path="/")


async def end_session(response: Response, redis: Redis, token: str | None) -> None:
    if token:
        await redis.delete(_key(token), _tokens_key(token))
    response.delete_cookie(COOKIE_NAME, path="/")


async def id_token_of(redis: Redis, token: str | None) -> str | None:
    stored = await redis.get(_tokens_key(token)) if token else None
    return json.loads(stored)["id_token"] if stored else None


async def _renew(token: str, redis: Redis, client: httpx.AsyncClient, settings: Settings) -> dict | None:
    """Renews expired tokens. Answers None once Keycloak has ended the session, and {} when nothing changed."""
    stored = await redis.get(_tokens_key(token))
    tokens = json.loads(stored) if stored else None
    if tokens is None or tokens["expires_at"] > time.time():
        return {}

    try:
        response = await client.post(
            f"{settings.kyc_authority}/protocol/openid-connect/token",
            data={"grant_type": "refresh_token", "refresh_token": tokens["refresh_token"]},
            auth=(settings.kyc_client_id, settings.shop_client_secret),
        )
    except httpx.HTTPError:
        # Keycloak being away is no reason to sign anyone out; the next request tries again
        return {}

    if response.status_code in (status.HTTP_400_BAD_REQUEST, status.HTTP_401_UNAUTHORIZED):
        return None
    if response.status_code != status.HTTP_200_OK:
        return {}

    answer = response.json()
    await redis.set(_tokens_key(token), session_tokens(answer), keepttl=True)
    return answer


async def current_user(
    token: str | None = Cookie(default=None, alias=COOKIE_NAME),
    redis: Redis = Depends(get_redis),
    db: AsyncSession = Depends(get_db),
    client: httpx.AsyncClient = Depends(get_http_client),
    settings: Settings = Depends(get_settings),
) -> User:
    user_id = await redis.get(_key(token)) if token else None
    user = await db.get(User, user_id) if user_id else None
    if user is None:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "Not signed in")

    renewed = await _renew(token, redis, client, settings)
    if renewed is None:
        await redis.delete(_key(token), _tokens_key(token))
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "Not signed in")

    name = claims_of(renewed["id_token"]).get("name") if renewed else None
    if name and name != user.name:
        user.name = name
        await db.commit()
    return user
