FROM redis:7-alpine

ENV TZ=Asia/Tashkent

EXPOSE 6379

CMD ["sh", "-c", "exec redis-server --appendonly yes --requirepass \"$REDIS_PASSWORD\""]
