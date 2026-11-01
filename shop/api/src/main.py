import asyncio
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager

from fastapi import FastAPI

from src.config import get_settings
from src.db import session_factory
from src.messages import consume_settled
from src.reconcile import reconcile_forever
from src.routers import auth, orders, products


@asynccontextmanager
async def lifespan(_: FastAPI) -> AsyncIterator[None]:
    settings = get_settings()
    tasks = [asyncio.create_task(reconcile_forever(session_factory, settings))]
    if settings.rabbit_url:
        tasks.append(asyncio.create_task(consume_settled(session_factory, settings)))
    yield
    for task in tasks:
        task.cancel()


app = FastAPI(title="Shop API", docs_url="/api/docs", openapi_url="/api/openapi.json", lifespan=lifespan)
app.include_router(auth.router)
app.include_router(products.router)
app.include_router(orders.router)


@app.get("/health", include_in_schema=False)
async def health() -> dict[str, str]:
    return {"status": "ok"}
