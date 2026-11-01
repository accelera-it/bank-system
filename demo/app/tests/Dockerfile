# Build context: demo/app (see demo/docker-compose.test.yml)
FROM mcr.microsoft.com/playwright:v1.64.1-noble
WORKDIR /app
COPY package.json package-lock.json playwright.config.ts ./
RUN npm ci
COPY tests tests
ENTRYPOINT ["npx", "playwright", "test"]
