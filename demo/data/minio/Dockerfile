FROM cgr.dev/chainguard/minio:latest
USER root
RUN mkdir -p /data && chown 65532:65532 /data
USER 65532
ENV TZ=Asia/Tashkent
EXPOSE 9000
CMD ["server", "/data"]
