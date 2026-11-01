import base64
import hashlib
import json
import secrets
import time
from typing import Literal
from urllib.parse import urlencode

import httpx
from fastapi import APIRouter, Cookie, Depends, HTTPException, Query, Response, status
from fastapi.responses import RedirectResponse
from redis.asyncio import Redis
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from src.cache import get_redis
from src.config import Settings, get_settings
from src.db import get_db
from src.models import Order, Product, User
from src.routers.orders import get_http_client
from src.schemas import LogoutOut, UserOut
from src.security import COOKIE_NAME, current_user, end_session, id_token_of, start_session

router = APIRouter(prefix="/api/auth", tags=["auth"])

LOGIN_SECONDS = 600
CALLBACK_PATH = "api/auth/callback"


def _login_key(state: str) -> str:
    return f"login:{state}"


def _local_path(next_path: str | None) -> str:
    """Keeps ?next= inside this site."""
    if next_path and next_path.startswith("/") and not next_path.startswith(("//", "/\\")):
        return next_path
    return "/cabinet"


def _claims(id_token: str) -> dict:
    """The token comes straight from Keycloak over the back channel, so its claims are read, not re-verified."""
    payload = id_token.split(".")[1]
    return json.loads(base64.urlsafe_b64decode(payload + "=" * (-len(payload) % 4)))


@router.get("/login", include_in_schema=False)
async def login(
    next: str | None = None,
    buy: str | None = None,
    register: bool = False,
    action: Literal["UPDATE_PROFILE", "UPDATE_PASSWORD"] | None = None,
    redis: Redis = Depends(get_redis),
    settings: Settings = Depends(get_settings),
) -> RedirectResponse:
    state = secrets.token_urlsafe(24)
    nonce = secrets.token_urlsafe(24)
    verifier = secrets.token_urlsafe(48)
    await redis.set(
        _login_key(state),
        json.dumps({"nonce": nonce, "verifier": verifier, "next": next, "buy": buy}),
        ex=LOGIN_SECONDS,
    )

    challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).rstrip(b"=").decode()
    query = {
        "client_id": settings.kyc_client_id,
        "redirect_uri": settings.site_url + CALLBACK_PATH,
        "response_type": "code",
        "scope": "openid email profile",
        "state": state,
        "nonce": nonce,
        "code_challenge": challenge,
        "code_challenge_method": "S256",
    }
    if register:
        query["prompt"] = "create"
    if action:
        query["kc_action"] = action

    url = f"{settings.kyc_public_authority}/protocol/openid-connect/auth?{urlencode(query)}"
    return RedirectResponse(url, status.HTTP_302_FOUND)


@router.get("/callback", include_in_schema=False)
async def callback(
    state: str = Query(),
    code: str | None = None,
    token: str | None = Cookie(default=None, alias=COOKIE_NAME),
    redis: Redis = Depends(get_redis),
    db: AsyncSession = Depends(get_db),
    settings: Settings = Depends(get_settings),
    client: httpx.AsyncClient = Depends(get_http_client),
) -> RedirectResponse:
    stored = await redis.getdel(_login_key(state))
    if stored is None:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "This login has expired")
    if code is None:
        return RedirectResponse(settings.site_url, status.HTTP_303_SEE_OTHER)

    login_data = json.loads(stored)
    response = await client.post(
        f"{settings.kyc_authority}/protocol/openid-connect/token",
        data={
            "grant_type": "authorization_code",
            "code": code,
            "redirect_uri": settings.site_url + CALLBACK_PATH,
            "code_verifier": login_data["verifier"],
        },
        auth=(settings.kyc_client_id, settings.shop_client_secret),
    )
    if response.status_code != status.HTTP_200_OK:
        raise HTTPException(status.HTTP_502_BAD_GATEWAY, "The login could not be completed")

    id_token = response.json()["id_token"]
    claims = _claims(id_token)
    if (
        claims.get("iss") != settings.kyc_public_authority
        or claims.get("aud") != settings.kyc_client_id
        or claims.get("nonce") != login_data["nonce"]
        or claims.get("exp", 0) <= time.time()
        or not claims.get("email")
    ):
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "Invalid login")

    user = await db.scalar(select(User).where(User.email == claims["email"]))
    if user is None:
        user = User(email=claims["email"])
        db.add(user)
    user.name = claims.get("name", "")
    await db.flush()

    target = _local_path(login_data["next"])
    product_id = login_data["buy"]
    if product_id and await db.get(Product, product_id) is not None:
        order = Order(product_id=product_id, user_id=user.id)
        db.add(order)
        await db.flush()
        target = f"/cabinet/orders?pay={order.id}"
    await db.commit()

    redirect = RedirectResponse(settings.site_url + target.lstrip("/"), status.HTTP_303_SEE_OTHER)
    await end_session(redirect, redis, token)
    await start_session(redirect, redis, user.id, id_token)
    return redirect


@router.post("/logout", response_model=LogoutOut)
async def logout(
    response: Response,
    token: str | None = Cookie(default=None, alias=COOKIE_NAME),
    redis: Redis = Depends(get_redis),
    settings: Settings = Depends(get_settings),
) -> LogoutOut:
    id_token = await id_token_of(redis, token)
    await end_session(response, redis, token)
    if id_token is None:
        return LogoutOut(url=settings.site_url)

    query = urlencode({"id_token_hint": id_token, "post_logout_redirect_uri": settings.site_url})
    return LogoutOut(url=f"{settings.kyc_public_authority}/protocol/openid-connect/logout?{query}")


@router.get("/me", response_model=UserOut)
async def me(user: User = Depends(current_user)) -> User:
    return user
