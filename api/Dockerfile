# syntax=docker/dockerfile:1
#
# NativeAOT binary on a chiseled runtime-deps image (no .NET runtime, no shell, non-root).
#
#   docker build --platform linux/amd64 -t alenalex-api .
#
# NativeAOT can't cross-compile between CPU architectures, so the build stage runs on
# the target platform (emulated when building amd64 on Apple Silicon).

# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH

# NativeAOT links with clang and needs zlib headers.
RUN apt-get update \
    && apt-get install -y --no-install-recommends clang zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

COPY src/AlenAlex.Api/AlenAlex.Api.csproj src/AlenAlex.Api/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    RID=linux-$([ "$TARGETARCH" = "arm64" ] && echo arm64 || echo x64) \
    && dotnet restore src/AlenAlex.Api/AlenAlex.Api.csproj -r "$RID"

COPY src/ src/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    RID=linux-$([ "$TARGETARCH" = "arm64" ] && echo arm64 || echo x64) \
    && dotnet publish src/AlenAlex.Api/AlenAlex.Api.csproj -c Release -r "$RID" --no-restore -o /app \
    && rm -f /app/*.dbg /app/*.staticwebassets.endpoints.json

# Runtime
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS final
LABEL org.opencontainers.image.source="https://github.com/AlenGeoAlex/alenalex.me" \
      org.opencontainers.image.description="alenalex.me API: guestbook, live status, post history"
WORKDIR /app
COPY --from=build /app ./

USER app
# Secrets come in as environment variables, e.g. -e Api__HashingSalt=... -e ConnectionStrings__Guestbook=...
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# No shell or curl in a chiseled image, so the binary health-checks itself.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD ["./alenalex-api", "--healthcheck"]

ENTRYPOINT ["./alenalex-api"]
