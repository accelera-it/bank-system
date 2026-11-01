from fastapi import APIRouter, Cookie, Depends, HTTPException, Response, status
from redis.asyncio import Redis
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from src.cache import get_redis
from src.db import get_db
from src.models import User
from src.schemas import Credentials, ProfileIn, Registration, UserOut
from src.security import (
    COOKIE_NAME,
    current_user,
    end_session,
    hash_password,
    start_session,
    verify_password,
)

router = APIRouter(prefix="/api/auth", tags=["auth"])


@router.post("/register", response_model=UserOut, status_code=status.HTTP_201_CREATED)
async def register(
    registration: Registration,
    response: Response,
    db: AsyncSession = Depends(get_db),
    redis: Redis = Depends(get_redis),
) -> User:
    if await db.scalar(select(User).where(User.email == registration.email)):
        raise HTTPException(status.HTTP_409_CONFLICT, "This email is already registered")

    user = User(
        email=registration.email,
        name=registration.name,
        password_hash=hash_password(registration.password),
    )
    db.add(user)
    await db.commit()
    await start_session(response, redis, user.id)
    return user


@router.post("/login", response_model=UserOut)
async def login(
    credentials: Credentials,
    response: Response,
    db: AsyncSession = Depends(get_db),
    redis: Redis = Depends(get_redis),
) -> User:
    user = await db.scalar(select(User).where(User.email == credentials.email))
    if user is None or not verify_password(credentials.password, user.password_hash):
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "Invalid email or password")

    await start_session(response, redis, user.id)
    return user


@router.post("/logout", status_code=status.HTTP_204_NO_CONTENT)
async def logout(
    response: Response,
    token: str | None = Cookie(default=None, alias=COOKIE_NAME),
    redis: Redis = Depends(get_redis),
) -> None:
    await end_session(response, redis, token)


@router.get("/me", response_model=UserOut)
async def me(user: User = Depends(current_user)) -> User:
    return user


@router.put("/me", response_model=UserOut)
async def update_profile(
    profile: ProfileIn,
    user: User = Depends(current_user),
    db: AsyncSession = Depends(get_db),
) -> User:
    user = await db.merge(user)
    user.name, user.phone, user.address = profile.name, profile.phone, profile.address
    await db.commit()
    return user
