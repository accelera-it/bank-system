# Shop

Guidance for DemoShop; the root `CLAUDE.md` has the overview and commands.

## MinIO (shop)
Product images uploaded in the cabinet go to the `products` bucket through `shop/api/src/storage.py`. The bucket is private: `shop_api` serves images at `/api/products/images/{key}`. The image is `cgr.dev/chainguard/minio`, because the official one is not on the registries. A product with orders cannot be deleted.
