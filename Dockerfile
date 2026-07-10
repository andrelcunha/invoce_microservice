# ──────────────────────────────────────────────────────────
# Stage 1 – build + publish + EF migrations bundle
# ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so restore layer is cached
COPY src/InvoiceMicroservice.Api/InvoiceMicroservice.Api.csproj                          src/InvoiceMicroservice.Api/
COPY src/InvoiceMicroservice.Application/InvoiceMicroservice.Application.csproj          src/InvoiceMicroservice.Application/
COPY src/InvoiceMicroservice.Domain/InvoiceMicroservice.Domain.csproj                    src/InvoiceMicroservice.Domain/
COPY src/InvoiceMicroservice.Infrastructure/InvoiceMicroservice.Infrastructure.csproj    src/InvoiceMicroservice.Infrastructure/
# RUN dotnet restore src/InvoiceMicroservice.Api/InvoiceMicroservice.Api.csproj

COPY src/ src/
RUN dotnet publish src/InvoiceMicroservice.Api/InvoiceMicroservice.Api.csproj \
    -c Release -o /app/publish

# Build self-contained EF migrations bundle
RUN dotnet tool install --global dotnet-ef
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet ef migrations bundle \
    --project src/InvoiceMicroservice.Infrastructure \
    --startup-project src/InvoiceMicroservice.Api \
    --output /app/efbundle \
    --self-contained \
    --force

# ──────────────────────────────────────────────────────────
# Stage 2 – runtime image
# ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app

COPY --from=build /app/publish .
COPY --from=build /app/efbundle .
COPY entrypoint.sh .
RUN chmod +x efbundle entrypoint.sh

ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["./entrypoint.sh"]
