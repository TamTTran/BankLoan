# ──────────────────────────────────────────────────────────────────────────────
# Stage 1 — Build React/Vite app
# Build context: repo root (so nginx.conf is accessible)
# ──────────────────────────────────────────────────────────────────────────────
FROM node:20-alpine AS build
WORKDIR /app

# --- Layer cache: install deps only when package files change ---
COPY frontend/package*.json ./
RUN npm install --prefer-offline

# --- Full source copy ---
COPY frontend/ ./

# Run unit tests before building (fail fast)
RUN npm run build

# ──────────────────────────────────────────────────────────────────────────────
# Stage 2 — Serve with Nginx (alpine = tiny image)
# ──────────────────────────────────────────────────────────────────────────────
FROM nginx:1.27-alpine AS runtime

# Remove default nginx config
RUN rm /etc/nginx/conf.d/default.conf

# Copy built static assets
COPY --from=build /app/dist /usr/share/nginx/html

# Copy custom nginx config (from repo root context)
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

# Nginx runs as non-root in official image already
# Override CMD to run in foreground
CMD ["nginx", "-g", "daemon off;"]
