#!/bin/sh
# Runs automatically at container start (official nginx image executes /docker-entrypoint.d/*.sh).
# Writes the runtime API URL into /env.js so the backend address can change without rebuilding the image.
# API_BASE_URL is used by the BROWSER, so it must be reachable from the user's machine (e.g. http://localhost:8080).
set -eu

value="${API_BASE_URL:-}"
escaped=$(printf '%s' "$value" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g')

printf 'window.__ENV__ = { API_BASE_URL: "%s" };\n' "$escaped" > /usr/share/nginx/html/env.js

echo "40-write-env.sh: API_BASE_URL=${value:-<empty: using the build-time VITE_API_BASE_URL>}"
