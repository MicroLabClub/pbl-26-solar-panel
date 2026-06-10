# syntax=docker/dockerfile:1

# --- Build stage ---
FROM node:20-alpine AS build
WORKDIR /app

# Install deps first (cached unless lockfile changes).
COPY package.json package-lock.json ./
RUN npm ci

# Build the static bundle.
COPY . ./
RUN npm run build

# --- Serve stage ---
FROM nginx:alpine AS runtime

# SPA fallback + /api reverse proxy to the backend service.
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
