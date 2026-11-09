# syntax=docker/dockerfile:1

# ---- deps: install exactly what package-lock.json says ----
FROM node:24-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

# ---- test: `docker compose --profile test run --rm frontend-tests` ----
FROM deps AS test
COPY . .
CMD ["npx", "vitest", "run"]

# ---- build: static files; VITE_API_BASE_URL is the build-time default ----
FROM deps AS build
COPY . .
ARG VITE_API_BASE_URL=http://localhost:8080
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
RUN npm run build

# ---- runtime: nginx; /env.js is written at container start from API_BASE_URL (no rebuild needed) ----
FROM nginx:1.27-alpine AS runtime
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY docker/40-write-env.sh /docker-entrypoint.d/40-write-env.sh
# Strip CR in case the repo was checked out with Windows line endings, then make it executable.
RUN sed -i 's/\r$//' /docker-entrypoint.d/40-write-env.sh && chmod +x /docker-entrypoint.d/40-write-env.sh
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
