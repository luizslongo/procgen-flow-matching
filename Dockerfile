# syntax=docker/dockerfile:1
# Multi-stage build for the PCG Flow Matching HTTP API (c4-api.pcg-flow-matching).
# Stage 1 publishes the app and bakes a throwaway random-weights checkpoint so the
# container can start and serve requests for security scanning (DAST) without the
# real 24MB trained checkpoint. Stage 2 is a slim non-root runtime image.

# ---------------------------------------------------------------------------
# Stage 1: build + publish + dummy checkpoint
# ---------------------------------------------------------------------------
# Ubuntu 24.04 (noble) base: its libstdc++ provides GLIBCXX_3.4.32, required by the
# TorchSharp 0.107 native runtime (libtorch). The default Debian bookworm image ships
# an older libstdc++ and fails to load LibTorchSharp.so.
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
WORKDIR /src

# libgomp1 is required by the TorchSharp-cpu native runtime to construct/save the model.
RUN apt-get update \
 && apt-get install -y --no-install-recommends libgomp1 \
 && rm -rf /var/lib/apt/lists/*

COPY . .

RUN dotnet publish c4-api.pcg-flow-matching/c4-api.pcg-flow-matching.csproj \
    -c Release -p:UseCPU=true -o /app/publish

# Random-weights checkpoint (hyperparameters must match config/api.docker.example.txt).
RUN dotnet /app/publish/c4-api.pcg-flow-matching.dll \
    init-dummy-checkpoint /app/publish/unet-baseline-checkpoint.bin 64 128 4

# ---------------------------------------------------------------------------
# Stage 2: runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/runtime:8.0-noble AS runtime

# libgomp1: TorchSharp-cpu native dependency. curl: container HEALTHCHECK probe.
RUN apt-get update \
 && apt-get install -y --no-install-recommends libgomp1 curl \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish ./
COPY config/api.docker.example.txt /app/config/api.txt

# Run as an unprivileged user (not root).
RUN useradd --create-home --uid 10001 appuser \
 && chown -R appuser:appuser /app
USER appuser

EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=3s --start-period=30s --retries=5 \
    CMD curl -fsS http://localhost:8080/status/health || exit 1

ENTRYPOINT ["dotnet", "c4-api.pcg-flow-matching.dll", "--config", "/app/config/api.txt"]
