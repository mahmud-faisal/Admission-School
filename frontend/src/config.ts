declare global {
  interface Window {
    /** Written at container start by the nginx entrypoint into /env.js (runtime override, no rebuild). */
    __ENV__?: { API_BASE_URL?: string };
  }
}

export const DEFAULT_API_BASE_URL = 'http://localhost:8080';

/**
 * Backend base URL. Priority: runtime /env.js (API_BASE_URL) → build-time VITE_API_BASE_URL → localhost:8080.
 * This URL is used by the BROWSER, so it must be reachable from the user's machine.
 */
export function apiBaseUrl(): string {
  const runtime = typeof window !== 'undefined' ? window.__ENV__?.API_BASE_URL?.trim() : undefined;
  const buildTime = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim();
  return (runtime || buildTime || DEFAULT_API_BASE_URL).replace(/\/+$/, '');
}
