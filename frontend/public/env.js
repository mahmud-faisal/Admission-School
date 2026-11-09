// Development default. In Docker, the nginx entrypoint overwrites this file with
// window.__ENV__ = { API_BASE_URL: "<value of the API_BASE_URL env var>" };
window.__ENV__ = window.__ENV__ || {};
