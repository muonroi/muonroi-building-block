# AOT-03: Alpine multi-stage Dockerfile for Muonroi.Pdf.AotSample (NativeAOT / linux-musl-x64)
# SC3 target: final image < 40 MB
# Final image size target: < 40 MB (NativeAOT binary + musl libc; font is embedded in the binary)
#
# Stage 1 — SDK build (NativeAOT compile)
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# NativeAOT prerequisites on Alpine: clang (LLVM linker), build-base (gcc/make), zlib-dev (compression)
RUN apk add --no-cache clang build-base zlib-dev

# Copy the full repo context (docker build context is repo root)
COPY . .

# Publish the AOT sample — produces a self-contained native binary in /app/publish
# -m:1 -nodereuse:false: MSBuild parallel-build flags for Docker layer isolation
RUN dotnet publish samples/Muonroi.Pdf.AotSample/Muonroi.Pdf.AotSample.csproj \
    -r linux-musl-x64 \
    -c Release \
    --self-contained \
    -o /app/publish \
    -m:1 -nodereuse:false

# Stage 2 — minimal runtime (only libc / libssl / libgcc stubs; no .NET runtime)
# No font packages required: the sample embeds SampleFont.ttf in the binary and serves it
# through its IFontResolver, so PdfSharpCore never consults the (empty) OS font store.
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["/app/Muonroi.Pdf.AotSample"]
