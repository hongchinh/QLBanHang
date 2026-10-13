# syntax=docker/dockerfile:1
# -----------------------------------------------------------------------------
# QLBanHang Frontend - Vite + React + Caddy
# Multi-stage build for Railway deployment.
# -----------------------------------------------------------------------------

# ---- Build stage ----
FROM node:20-alpine AS build
WORKDIR /app

# VITE_* vars must be present at build time — Vite inlines them into the bundle.
ARG VITE_API_BASE_URL
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

COPY package.json package-lock.json ./
# Use `npm install` (not `npm ci`) because vitest@4 peers on vite@6+ but the
# project pins vite@5 — npm 10 (Railway) and npm 11 (local) resolve the
# lockfile differently, so `npm ci` fails on Railway with "Missing: esbuild@0.28".
# `--no-audit --no-fund` keep build logs clean.
RUN npm install --no-audit --no-fund --legacy-peer-deps

COPY . .
RUN npm run build

# ---- Runtime stage (Caddy) ----
FROM caddy:2-alpine AS runtime
WORKDIR /srv

COPY --from=build /app/dist ./
COPY Caddyfile /etc/caddy/Caddyfile

# Railway injects $PORT at runtime; Caddyfile reads it via {$PORT}.
ENV PORT=8080
EXPOSE 8080

CMD ["caddy", "run", "--config", "/etc/caddy/Caddyfile", "--adapter", "caddyfile"]
