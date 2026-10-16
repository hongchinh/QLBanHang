# syntax=docker/dockerfile:1
# -----------------------------------------------------------------------------
# QLBanHang Backend - .NET 10 Web API
# Multi-stage build for Railway deployment.
# -----------------------------------------------------------------------------

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution + props + csproj first to leverage layer cache for `dotnet restore`.
COPY OrderMgmt.sln Directory.Build.props ./
COPY src/OrderMgmt.Domain/OrderMgmt.Domain.csproj           src/OrderMgmt.Domain/
COPY src/OrderMgmt.Application/OrderMgmt.Application.csproj src/OrderMgmt.Application/
COPY src/OrderMgmt.Infrastructure/OrderMgmt.Infrastructure.csproj src/OrderMgmt.Infrastructure/
COPY src/OrderMgmt.WebApi/OrderMgmt.WebApi.csproj           src/OrderMgmt.WebApi/

RUN dotnet restore src/OrderMgmt.WebApi/OrderMgmt.WebApi.csproj

# Copy the rest of the source and publish.
COPY src/ ./src/
RUN dotnet publish src/OrderMgmt.WebApi/OrderMgmt.WebApi.csproj \
        -c Release \
        -o /app/publish \
        --no-restore \
        /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# LibreOffice (headless) is required by QuotationExport PDF rendering.
# Ubuntu 24.04: ttf-mscorefonts-installer is in multiverse; the EULA must be accepted up front.
RUN sed -i 's/^Components: .*/Components: main restricted universe multiverse/' /etc/apt/sources.list.d/ubuntu.sources \
 && echo "ttf-mscorefonts-installer msttcorefonts/accepted-mscorefonts-eula select true" | debconf-set-selections \
 && apt-get update \
 && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
        libreoffice-calc \
        libreoffice-core \
        fontconfig \
        fonts-dejavu \
        fonts-liberation \
        fonts-noto \
        fonts-noto-cjk \
        ttf-mscorefonts-installer \
        locales \
 && locale-gen vi_VN.UTF-8 \
 && rm -rf /var/lib/apt/lists/*

# LibreOffice prints numbers with the separators of its locale. The wrapper runs it in vi_VN, so PDFs
# show 1.250.000 like Excel on a Vietnamese Windows. Only soffice gets the locale, not the .NET process.
RUN printf '#!/bin/sh\nLC_ALL=vi_VN.UTF-8 LANG=vi_VN.UTF-8 exec /usr/bin/soffice "$@"\n' > /usr/local/bin/soffice-vi \
 && chmod +x /usr/local/bin/soffice-vi

COPY fonts/ /usr/share/fonts/truetype/project/
RUN fc-cache -fv

COPY --from=build /app/publish ./

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_GCServer=1 \
    PORT=8080 \
    QuotationExport__LibreOfficePath=/usr/local/bin/soffice-vi

# Railway injects $PORT at runtime. ASP.NET Core listens on ASPNETCORE_URLS,
# so we expand $PORT into the URL at container start.
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT} exec dotnet OrderMgmt.WebApi.dll"]
