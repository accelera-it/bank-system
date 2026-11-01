from fastapi import FastAPI

from src.routers import auth, orders, products

app = FastAPI(title="Shop API", docs_url="/api/docs", openapi_url="/api/openapi.json")
app.include_router(auth.router)
app.include_router(products.router)
app.include_router(orders.router)


@app.get("/health", include_in_schema=False)
async def health() -> dict[str, str]:
    return {"status": "ok"}
