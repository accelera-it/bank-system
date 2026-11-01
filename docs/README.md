# Docs

| File | Contents
| - | -
| `infra.png` | Architecture diagram
| `tech.pdf` | Infrastructure and architecture overview
| `slide.pdf` | One-page product overview
| `access.txt` | Default accounts
| `rsa-keygen.sh` | Generates RSA key pairs in the .NET XML format
| `src/` | HTML sources of the first three; `build_tech.py` writes `tech.html`

To render them again, with the image the shop's UI tests build:

```bash
python3 docs/src/build_tech.py
docker compose -f shop/docker-compose.test.yml build shop_ui_tests
docker run --rm -v "$PWD/docs:/docs" --entrypoint node shop-test-shop_ui_tests /docs/src/render.mjs
```
