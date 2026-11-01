import json
import uuid

from fastapi import APIRouter, Depends, File, HTTPException, Path, Response, UploadFile, status
from fastapi.concurrency import run_in_threadpool
from redis.asyncio import Redis
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from src.cache import get_redis
from src.config import get_settings
from src.db import get_db
from src.models import Product, User
from src.schemas import ImageOut, ProductIn, ProductOut
from src.security import current_user
from src.storage import Storage, get_storage

router = APIRouter(prefix="/api/products", tags=["products"])

CACHE_KEY = "products:all"
MAX_IMAGE_BYTES = 5 * 1024 * 1024
IMAGE_KEY = r"^[0-9a-f]{32}\.(png|jpg|webp)$"


def detect_image(data: bytes) -> tuple[str, str] | None:
    """The stored type comes from the bytes, so a page uploaded as an image cannot be served as one."""
    if data.startswith(b"\x89PNG\r\n\x1a\n"):
        return "png", "image/png"
    if data.startswith(b"\xff\xd8\xff"):
        return "jpg", "image/jpeg"
    if data[:4] == b"RIFF" and data[8:12] == b"WEBP":
        return "webp", "image/webp"
    return None


@router.get("", response_model=list[ProductOut])
async def list_products(db: AsyncSession = Depends(get_db), redis: Redis = Depends(get_redis)) -> list:
    cached = await redis.get(CACHE_KEY)
    if cached:
        return json.loads(cached)

    products = (await db.scalars(select(Product).order_by(Product.name))).all()
    result = [ProductOut.model_validate(p).model_dump() for p in products]
    await redis.set(CACHE_KEY, json.dumps(result), ex=get_settings().products_cache_seconds)
    return result


@router.post("/images", response_model=ImageOut, status_code=status.HTTP_201_CREATED)
async def upload_image(
    file: UploadFile = File(),
    _: User = Depends(current_user),
    storage: Storage = Depends(get_storage),
) -> ImageOut:
    data = await file.read(MAX_IMAGE_BYTES + 1)
    if len(data) > MAX_IMAGE_BYTES:
        raise HTTPException(status.HTTP_413_CONTENT_TOO_LARGE, "The image must be at most 5 MB")

    image = detect_image(data)
    if image is None:
        raise HTTPException(status.HTTP_415_UNSUPPORTED_MEDIA_TYPE, "Use a PNG, JPEG or WebP image")

    extension, content_type = image
    key = f"{uuid.uuid4().hex}.{extension}"
    await run_in_threadpool(storage.put, key, data, content_type)
    return ImageOut(url=f"/api/products/images/{key}")


@router.get("/images/{key}")
async def get_image(key: str = Path(pattern=IMAGE_KEY), storage: Storage = Depends(get_storage)) -> Response:
    image = await run_in_threadpool(storage.get, key)
    if image is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Image not found")

    data, content_type = image
    headers = {"Cache-Control": "public, max-age=31536000, immutable", "X-Content-Type-Options": "nosniff"}
    return Response(data, media_type=content_type, headers=headers)


@router.get("/{product_id}", response_model=ProductOut)
async def get_product(product_id: str, db: AsyncSession = Depends(get_db)) -> Product:
    product = await db.get(Product, product_id)
    if product is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Product not found")
    return product


@router.post("", response_model=ProductOut, status_code=status.HTTP_201_CREATED)
async def create_product(
    model: ProductIn,
    _: User = Depends(current_user),
    db: AsyncSession = Depends(get_db),
    redis: Redis = Depends(get_redis),
) -> Product:
    product = Product(**model.model_dump())
    db.add(product)
    await db.commit()
    await redis.delete(CACHE_KEY)
    return product
